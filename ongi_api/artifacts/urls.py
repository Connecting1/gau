# artifacts/urls.py

from django.urls import path
from .views import (
    artifact_list_view,
    artifact_detail_view,
    artifact_update_view,
    artifact_feeds_view,
    # ✨ 새로 추가
    generate_artifact_description_stream,
    get_artifact_description,
    regenerate_artifact_description,
    generate_ai_narrative,
)

urlpatterns = [
    path('', artifact_list_view, name='artifact-list'),
    path('<uuid:artifact_id>/', artifact_detail_view, name='artifact-detail'),
    path('<uuid:artifact_id>/update/', artifact_update_view, name='artifact-update'),
    path('<uuid:artifact_id>/feeds/', artifact_feeds_view, name='artifact-feeds'),

    # ✨ AI 설명 생성 API
    path('<uuid:artifact_id>/generate-description/',
         generate_artifact_description_stream,
         name='generate-artifact-description'),
    path('<uuid:artifact_id>/description/',
         get_artifact_description,
         name='get-artifact-description'),
    path('<uuid:artifact_id>/regenerate-description/',
         regenerate_artifact_description,
         name='regenerate-artifact-description'),

    # ✨ AI 서술형 설명 자동 생성
    path('generate-narrative/',
         generate_ai_narrative,
         name='generate-ai-narrative'),
]