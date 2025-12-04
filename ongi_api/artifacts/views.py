# artifacts/views.py

from django.shortcuts import get_object_or_404
from django.http import StreamingHttpResponse
from rest_framework import status
from rest_framework.decorators import api_view, permission_classes
from rest_framework.permissions import IsAuthenticated, IsAdminUser
from rest_framework.response import Response
from .models import Artifact
from .services import OllamaService
from .ai_narrative_service import AiNarrativeService
from django.utils import timezone
import json
from .serializers import ArtifactSerializer


@api_view(['GET'])
def artifact_list_view(request):
    """유물 목록 조회"""
    # 상태에 따른 필터링 (기본: 검증된 유물)
    status_filter = request.query_params.get('status', 'verified')
    
    if status_filter == 'all' and request.user.is_staff:
        # 관리자는 모든 상태의 유물 조회 가능
        artifacts = Artifact.objects.all().order_by('-created_at')
    elif status_filter == 'all':
        # 일반 사용자는 자동생성 + 검증됨 + 주목할만한 유물만 조회 가능
        artifacts = Artifact.objects.exclude(status='rejected').order_by('-created_at')
    else:
        # 특정 상태의 유물만 조회
        artifacts = Artifact.objects.filter(status=status_filter).order_by('-created_at')
    
    serializer = ArtifactSerializer(artifacts, many=True)
    return Response(serializer.data)

@api_view(['GET'])
def artifact_detail_view(request, artifact_id):
    """유물 상세 정보 조회"""
    artifact = get_object_or_404(Artifact, id=artifact_id)
    
    # 거부된 유물은 관리자만 조회 가능
    if artifact.status == 'rejected' and not request.user.is_staff:
        return Response({"detail": "접근 권한이 없습니다."}, status=status.HTTP_403_FORBIDDEN)
    
    serializer = ArtifactDetailSerializer(artifact)
    return Response(serializer.data)

@api_view(['PUT', 'PATCH'])
@permission_classes([IsAuthenticated, IsAdminUser])
def artifact_update_view(request, artifact_id):
    """유물 정보 업데이트 (관리자 전용)"""
    artifact = get_object_or_404(Artifact, id=artifact_id)
    
    serializer = ArtifactSerializer(artifact, data=request.data, partial=True)
    if serializer.is_valid():
        serializer.save()
        return Response(serializer.data)
    return Response(serializer.errors, status=status.HTTP_400_BAD_REQUEST)

@api_view(['GET'])
def artifact_feeds_view(request, artifact_id):
    """특정 유물과 관련된 피드 목록 조회"""
    artifact = get_object_or_404(Artifact, id=artifact_id)
    
    # 거부된 유물은 관리자만 조회 가능
    if artifact.status == 'rejected' and not request.user.is_staff:
        return Response({"detail": "접근 권한이 없습니다."}, status=status.HTTP_403_FORBIDDEN)
    
    # 페이지네이션 파라미터
    page_size = int(request.query_params.get('page_size', 10))
    page = int(request.query_params.get('page', 1))
    
    # 유물과 연결된 피드 찾기
    artifact_feeds = ArtifactFeed.objects.filter(artifact=artifact).select_related('feed')
    
    # 페이지네이션 적용
    start_idx = (page - 1) * page_size
    end_idx = start_idx + page_size
    paginated_feeds = artifact_feeds[start_idx:end_idx]
    
    # 피드 목록 추출 및 직렬화
    feeds = [item.feed for item in paginated_feeds]
    serializer = FeedSerializer(feeds, many=True)
    
    response_data = {
        'results': serializer.data,
        'count': artifact_feeds.count(),
        'page': page,
        'page_size': page_size,
        'total_pages': (artifact_feeds.count() + page_size - 1) // page_size
    }
    
    return Response(response_data)

@api_view(['POST'])
def generate_artifact_description_stream(request, artifact_id):
    """
    유물 설명 AI 생성 (SSE 스트리밍)
    
    POST /api/artifacts/{artifact_id}/generate-description/
    """
    artifact = get_object_or_404(Artifact, id=artifact_id)
    
    def event_stream():
        """SSE 이벤트 스트림"""
        full_description = ""
        
        try:
            # Ollama 스트리밍 생성
            for chunk in OllamaService.generate_artifact_description(
                artifact_name=artifact.name,
                time_period=artifact.time_period,
                estimated_year=artifact.estimated_year,
                origin_location=artifact.origin_location,
                stream=True
            ):
                if 'chunk' in chunk:
                    full_description += chunk['chunk']
                    # SSE 형식으로 전송
                    yield f"data: {json.dumps({'type': 'chunk', 'content': chunk['chunk']}, ensure_ascii=False)}\n\n"
                
                elif 'error' in chunk:
                    yield f"data: {json.dumps({'type': 'error', 'message': chunk['error']}, ensure_ascii=False)}\n\n"
                    return
                
                elif chunk.get('done'):
                    # DB에 저장
                    artifact.ai_description = full_description
                    artifact.ai_description_generated_at = timezone.now()
                    artifact.save(update_fields=['ai_description', 'ai_description_generated_at'])
                    
                    yield f"data: {json.dumps({'type': 'complete', 'full_text': full_description}, ensure_ascii=False)}\n\n"
                    break
        
        except Exception as e:
            yield f"data: {json.dumps({'type': 'error', 'message': str(e)}, ensure_ascii=False)}\n\n"
    
    response = StreamingHttpResponse(
        event_stream(),
        content_type='text/event-stream',
    )
    response['Cache-Control'] = 'no-cache'
    response['X-Accel-Buffering'] = 'no'
    response['Access-Control-Allow-Origin'] = '*'
    return response


@api_view(['GET'])
def get_artifact_description(request, artifact_id):
    """
    유물 설명 조회 (캐시된 AI 설명 우선)
    
    GET /api/artifacts/{artifact_id}/description/
    
    Response:
    {
        "description": "설명 텍스트",
        "is_ai_generated": true,
        "generated_at": "2025-01-15T12:00:00Z",
        "needs_regeneration": false
    }
    """
    artifact = get_object_or_404(Artifact, id=artifact_id)
    
    # AI 생성 설명이 있으면 우선 사용
    if artifact.ai_description:
        description = artifact.ai_description
        is_ai = True
    else:
        description = artifact.description or "설명이 없습니다."
        is_ai = False
    
    return Response({
        'description': description,
        'is_ai_generated': is_ai,
        'generated_at': artifact.ai_description_generated_at,
        'needs_regeneration': artifact.needs_ai_description(),
        'artifact_name': artifact.name,
        'time_period': artifact.time_period,
    })


@api_view(['POST'])
def regenerate_artifact_description(request, artifact_id):
    """
    유물 설명 강제 재생성

    POST /api/artifacts/{artifact_id}/regenerate-description/
    """
    artifact = get_object_or_404(Artifact, id=artifact_id)

    # 기존 설명 삭제
    artifact.ai_description = None
    artifact.ai_description_generated_at = None
    artifact.save(update_fields=['ai_description', 'ai_description_generated_at'])

    return Response({
        'message': 'AI 설명이 초기화되었습니다. 새로 생성해주세요.',
        'artifact_id': str(artifact.id),
    })


@api_view(['POST'])
def generate_ai_narrative(request):
    """
    유물 이름으로 AI 서술형 설명 생성

    POST /api/artifacts/generate-narrative/
    Body:
    {
        "artifact_name": "첨성대",
        "use_openai": true  // optional, default: true
    }

    Response:
    {
        "artifact_name": "첨성대",
        "narrative": "신라의 밤하늘을 향해...",
        "method": "openai" or "template"
    }
    """
    artifact_name = request.data.get('artifact_name')
    use_openai = request.data.get('use_openai', True)

    if not artifact_name:
        return Response(
            {'error': 'artifact_name is required'},
            status=status.HTTP_400_BAD_REQUEST
        )

    # AI 서술형 설명 생성
    narrative = AiNarrativeService.generate_narrative(
        artifact_name=artifact_name,
        use_openai=use_openai
    )

    # 생성 방법 확인
    method = "template"
    if use_openai and "천 년의 시간" not in narrative and "문화유산입니다" not in narrative:
        method = "openai"

    return Response({
        'artifact_name': artifact_name,
        'narrative': narrative,
        'method': method,
    })