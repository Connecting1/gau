"""
AI 서술형 설명 자동 생성 서비스
유물 이름을 기반으로 소설처럼 서술된 설명을 생성합니다.
Ollama llama3.1:8b 모델 사용 + Wikipedia 정보 기반 RAG
"""
import logging
from typing import Optional, Dict
import requests

logger = logging.getLogger(__name__)


class AiNarrativeService:
    """AI 서술형 설명 생성 서비스 (Ollama + RAG 기반)"""

    @staticmethod
    def search_wikipedia(artifact_name: str) -> Optional[str]:
        """
        Wikipedia에서 유물 정보 검색

        Args:
            artifact_name: 유물 이름

        Returns:
            Wikipedia 요약 텍스트 또는 None
        """
        try:
            # 한국어 Wikipedia API 사용
            search_url = "https://ko.wikipedia.org/w/api.php"

            # 1단계: 검색하여 페이지 제목 찾기
            search_params = {
                "action": "query",
                "format": "json",
                "list": "search",
                "srsearch": artifact_name,
                "utf8": 1,
                "srlimit": 1,
            }

            search_response = requests.get(search_url, params=search_params, timeout=10)
            search_data = search_response.json()

            if not search_data.get("query", {}).get("search"):
                logger.warning(f"No Wikipedia results for '{artifact_name}'")
                return None

            # 가장 관련성 높은 페이지 제목
            page_title = search_data["query"]["search"][0]["title"]

            # 2단계: 페이지 내용 추출
            content_params = {
                "action": "query",
                "format": "json",
                "prop": "extracts",
                "exintro": True,  # 도입부만
                "explaintext": True,  # 일반 텍스트
                "titles": page_title,
                "utf8": 1,
            }

            content_response = requests.get(search_url, params=content_params, timeout=10)
            content_data = content_response.json()

            pages = content_data.get("query", {}).get("pages", {})
            if not pages:
                return None

            # 첫 번째 페이지의 내용 추출
            page = next(iter(pages.values()))
            extract = page.get("extract", "")

            if extract:
                # 너무 길면 앞부분만 사용 (500자 정도)
                if len(extract) > 500:
                    extract = extract[:500] + "..."

                logger.info(f"Retrieved Wikipedia info for '{artifact_name}': {len(extract)} characters")
                return extract

            return None

        except Exception as e:
            logger.error(f"Error searching Wikipedia: {e}")
            return None

    @staticmethod
    def _create_narrative_prompt(artifact_name: str, wikipedia_info: Optional[str] = None) -> str:
        """
        서술형 스타일 프롬프트 생성 (RAG 방식)

        Args:
            artifact_name: 유물 이름
            wikipedia_info: Wikipedia에서 검색한 정보

        Returns:
            프롬프트 문자열
        """
        prompt = f"""당신은 한국 문화유산 전문 해설가입니다.
주어진 유물에 대해 **소설처럼 아름답고 서술적인** 설명을 작성해주세요.

📌 유물 이름: {artifact_name}
"""

        if wikipedia_info:
            prompt += f"""
📚 참고 정보 (Wikipedia):
{wikipedia_info}

⚠️ 위 정보를 기반으로 **사실을 정확히** 반영하되, **서정적이고 감성적인 문체**로 재해석하세요.
"""

        prompt += """
✍️ 작성 규칙:
1. **3개의 단락**으로 구성 (각 단락 2-3문장)
2. **첫 단락**: 유물의 역사적 배경과 시대적 맥락을 **시적으로** 서술
   예: "천 년의 세월을 견디며...", "역사의 숨결이 깃든..."
3. **둘째 단락**: 건축/제작 특징을 **비유와 감탄**을 담아 표현
   예: "정교하게 쌓아 올린 우아한 곡선...", "장인의 혼이 담긴..."
4. **셋째 단락**: 현재까지의 의미를 **감동적으로** 마무리
   예: "지금도 우뚝 서서...", "우리에게 전하고 있습니다"

🎨 문체 스타일:
- **서정적이고 품격있는 문어체** 사용
- **비유, 은유, 감탄** 활용
- **마치 소설을 읽는 듯한 느낌**
- 존댓말 사용하지 않고 평서문으로
- 총 **200-300자** 정도

❌ 금지사항:
- 건조한 설명문 X
- "~입니다", "~합니다" 같은 딱딱한 표현 최소화
- 할루네이션 X (참고 정보에 없는 내용 지어내지 말 것)

📝 예시 문체:
"신라의 밤하늘을 향해 천 년의 시간을 견뎌온 첨성대. 선덕여왕 시대에 세워진 이 천문 관측대는 동양에서 가장 오래된 현존하는 천문대로, 시간을 초월한 아름다움을 간직하고 있다.

362개의 화강암을 정교하게 쌓아 올린 우아한 곡선은 단순한 건축미를 넘어, 당시 신라인들의 놀라운 과학적 지혜를 담고 있다. 높이 9.17미터의 이 탑은 일년의 날수와 24절기를 상징하며, 하늘과 땅을 잇는 우주론적 의미를 간직하고 있다.

천년이 지난 지금도 경주 들판에 우뚝 서서, 별을 관측하던 신라 천문학자들의 열정과 지혜를 우리에게 전하고 있다."

위와 같은 **시적이고 서정적인 문체**로 {artifact_name}에 대한 AI 해설을 작성하세요:"""

        return prompt

    @staticmethod
    def generate_narrative_with_ollama(artifact_name: str) -> Optional[str]:
        """
        Ollama를 사용하여 AI 서술형 설명 생성 (RAG 방식)

        Args:
            artifact_name: 유물 이름

        Returns:
            생성된 서술형 설명 또는 None
        """
        try:
            # OllamaService import (순환 참조 방지를 위해 함수 내에서 import)
            from .services import OllamaService
            import httpx
            import json

            # 1단계: Wikipedia에서 실제 정보 검색 (RAG)
            logger.info(f"Searching Wikipedia for '{artifact_name}'...")
            wikipedia_info = AiNarrativeService.search_wikipedia(artifact_name)

            if wikipedia_info:
                logger.info(f"Wikipedia info retrieved: {len(wikipedia_info)} characters")
            else:
                logger.warning(f"No Wikipedia info found for '{artifact_name}', proceeding without RAG")

            # 2단계: 서술형 프롬프트 생성 (Wikipedia 정보 포함)
            prompt = AiNarrativeService._create_narrative_prompt(artifact_name, wikipedia_info)

            # 3단계: Ollama API 호출
            full_text = ""

            with httpx.stream(
                'POST',
                f'{OllamaService.OLLAMA_BASE_URL}/api/generate',
                json={
                    'model': OllamaService.DEFAULT_MODEL,
                    'prompt': prompt,
                    'stream': True,
                    'options': {
                        'temperature': 0.7,
                        'top_p': 0.9,
                        'num_predict': 500,
                    }
                },
                timeout=60.0
            ) as response:

                if response.status_code != 200:
                    logger.error(f'Ollama API error: {response.status_code}')
                    return None

                for line in response.iter_lines():
                    if line.strip():
                        try:
                            data = json.loads(line)

                            if 'response' in data:
                                full_text += data['response']

                            if data.get('done', False):
                                break

                        except json.JSONDecodeError as e:
                            logger.error(f'JSON decode error: {str(e)}')
                            continue

            narrative = full_text.strip()
            logger.info(f"Generated narrative for '{artifact_name}': {len(narrative)} characters")

            return narrative if narrative else None

        except Exception as e:
            logger.error(f"Error generating narrative with Ollama: {e}")
            return None

    @staticmethod
    def generate_narrative_template(artifact_name: str) -> str:
        """
        템플릿 기반 서술형 설명 생성 (Fallback)

        Args:
            artifact_name: 유물 이름

        Returns:
            템플릿 기반 서술형 설명
        """
        # 유물별 템플릿 (하드코딩)
        templates = {
            "첨성대": """신라의 밤하늘을 향해 천 년의 시간을 견뎌온 첨성대. 선덕여왕 시대인 632년부터 647년 사이에 세워진 이 천문 관측대는 동양에서 가장 오래된 현존하는 천문대입니다.

362개의 화강암을 정교하게 쌓아 올린 우아한 곡선은 단순한 건축미를 넘어, 당시 신라인들의 놀라운 과학적 지혜를 담고 있습니다. 높이 9.17미터의 이 탑은 일년의 날수와 24절기를 상징하며, 하늘과 땅을 잇는 우주론적 의미를 간직하고 있습니다.

천년이 지난 지금도 경주 들판에 우뚝 서서, 별을 관측하던 신라 천문학자들의 열정과 지혜를 우리에게 전하고 있습니다.""",
        }

        # 유물 이름 정규화
        normalized_name = artifact_name.strip().lower()

        # 템플릿에서 찾기
        for key, template in templates.items():
            if key.lower() in normalized_name or normalized_name in key.lower():
                return template

        # 템플릿이 없으면 기본 메시지
        return f"""{artifact_name}은(는) 우리의 소중한 문화유산입니다.

선조들의 지혜와 기술이 담긴 이 유물은 시간을 넘어 오늘날까지 그 가치를 인정받고 있습니다.

앞으로도 이 문화유산이 후대에 잘 전승되어, 우리의 역사와 정체성을 이어가는 소중한 매개체가 되기를 바랍니다."""

    @staticmethod
    def generate_narrative(artifact_name: str, use_ai: bool = True) -> str:
        """
        AI 서술형 설명 생성

        Args:
            artifact_name: 유물 이름
            use_ai: Ollama AI 사용 여부 (기본값: True)

        Returns:
            생성된 서술형 설명
        """
        if not artifact_name:
            return ""

        # Ollama 사용 시도
        if use_ai:
            narrative = AiNarrativeService.generate_narrative_with_ollama(artifact_name)
            if narrative:
                return narrative
            logger.info(f"Falling back to template for '{artifact_name}'")

        # Fallback: 템플릿 사용
        return AiNarrativeService.generate_narrative_template(artifact_name)
