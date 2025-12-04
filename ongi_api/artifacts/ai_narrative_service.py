"""
AI 서술형 설명 자동 생성 서비스
유물 이름을 기반으로 소설처럼 서술된 설명을 생성합니다.
"""
import os
import logging
from typing import Optional

logger = logging.getLogger(__name__)


class AiNarrativeService:
    """AI 서술형 설명 생성 서비스"""

    # 서술 스타일 프롬프트
    NARRATIVE_STYLE_PROMPT = """당신은 한국 문화유산 전문 해설가입니다.
주어진 유물 이름에 대해 소설처럼 아름답고 서술적인 설명을 작성해주세요.

작성 규칙:
1. 3개의 단락으로 구성 (각 단락은 2-3문장)
2. 첫 단락: 유물의 역사적 배경과 시대적 의미
3. 둘째 단락: 유물의 건축/제작 특징과 과학적/예술적 가치
4. 셋째 단락: 현재까지 이어진 문화적 의미와 감동
5. 문체: 서정적이고 품격있는 문어체 사용
6. 사실을 기반으로 하되, 감성적으로 서술
7. 총 200-300자 정도의 분량

유물 이름: {artifact_name}

위 유물에 대한 AI 해설을 작성해주세요:"""

    @staticmethod
    def generate_narrative_with_openai(artifact_name: str) -> Optional[str]:
        """
        OpenAI API를 사용하여 AI 서술형 설명 생성

        Args:
            artifact_name: 유물 이름

        Returns:
            생성된 서술형 설명 또는 None
        """
        try:
            # OpenAI API 키 확인
            api_key = os.environ.get('OPENAI_API_KEY')
            if not api_key:
                logger.warning("OPENAI_API_KEY not found in environment")
                return None

            # OpenAI 클라이언트 import
            try:
                from openai import OpenAI
            except ImportError:
                logger.error("openai package not installed. Install with: pip install openai")
                return None

            # OpenAI 클라이언트 생성
            client = OpenAI(api_key=api_key)

            # 프롬프트 생성
            prompt = AiNarrativeService.NARRATIVE_STYLE_PROMPT.format(
                artifact_name=artifact_name
            )

            # API 호출
            response = client.chat.completions.create(
                model="gpt-4o-mini",  # 비용 효율적인 모델
                messages=[
                    {
                        "role": "system",
                        "content": "당신은 한국 문화유산 전문 해설가입니다."
                    },
                    {
                        "role": "user",
                        "content": prompt
                    }
                ],
                max_tokens=500,
                temperature=0.7,
            )

            # 응답 추출
            narrative = response.choices[0].message.content.strip()
            logger.info(f"Generated narrative for '{artifact_name}': {len(narrative)} characters")

            return narrative

        except Exception as e:
            logger.error(f"Error generating narrative with OpenAI: {e}")
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
    def generate_narrative(artifact_name: str, use_openai: bool = True) -> str:
        """
        AI 서술형 설명 생성

        Args:
            artifact_name: 유물 이름
            use_openai: OpenAI API 사용 여부 (기본값: True)

        Returns:
            생성된 서술형 설명
        """
        if not artifact_name:
            return ""

        # OpenAI 사용 시도
        if use_openai:
            narrative = AiNarrativeService.generate_narrative_with_openai(artifact_name)
            if narrative:
                return narrative
            logger.info(f"Falling back to template for '{artifact_name}'")

        # Fallback: 템플릿 사용
        return AiNarrativeService.generate_narrative_template(artifact_name)
