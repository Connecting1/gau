import '../services/ai_narrative_service.dart';

/// AI 서술형 설명을 제공하는 유틸리티
/// 백엔드 API를 통해 유물 이름 기반으로 서술형 설명을 자동 생성합니다.
class AiNarrativeProvider {
  /// 생성 중인 유물들을 추적하여 중복 요청 방지
  static final Set<String> _generatingArtifacts = {};

  /// 유물 이름에 따른 AI 서술형 설명을 비동기로 가져오기
  ///
  /// [artifactName] 유물 이름
  /// [onGenerated] 생성 완료 콜백 (선택사항)
  ///
  /// 반환값: AI 서술형 텍스트 Future
  static Future<String?> getNarrative(
    String? artifactName, {
    Function(String narrative)? onGenerated,
  }) async {
    if (artifactName == null || artifactName.isEmpty) return null;

    // 이미 생성 중이면 대기
    if (_generatingArtifacts.contains(artifactName)) {
      return null;
    }

    // 생성 중 표시
    _generatingArtifacts.add(artifactName);

    try {
      // 백엔드 API로 AI 서술형 설명 생성
      final narrative = await AiNarrativeService.generateNarrative(
        artifactName,
        useCache: true,
        useOpenai: true,
      );

      if (narrative != null && onGenerated != null) {
        onGenerated(narrative);
      }

      return narrative;
    } finally {
      // 생성 완료 표시
      _generatingArtifacts.remove(artifactName);
    }
  }

  /// 동기 방식으로 설명 가져오기 (Fallback - 템플릿 사용)
  ///
  /// [artifactName] 유물 이름
  /// 반환값: 템플릿 기반 서술형 텍스트
  static String? getNarrativeSync(String? artifactName) {
    if (artifactName == null) return null;

    // 유물 이름을 소문자로 변환하여 매칭 (공백 제거)
    final normalizedName = artifactName.toLowerCase().replaceAll(' ', '');

    switch (normalizedName) {
      case '첨성대':
      case 'cheomseongdae':
        return _getCheomseongdaeNarrative();

      // 다른 유물들도 추가 가능
      default:
        return null;
    }
  }

  /// 첨성대에 대한 AI 서술형 설명 (템플릿)
  static String _getCheomseongdaeNarrative() {
    return '''신라의 밤하늘을 향해 천 년의 시간을 견뎌온 첨성대. 선덕여왕 시대인 632년부터 647년 사이에 세워진 이 천문 관측대는 동양에서 가장 오래된 현존하는 천문대입니다.

362개의 화강암을 정교하게 쌓아 올린 우아한 곡선은 단순한 건축미를 넘어, 당시 신라인들의 놀라운 과학적 지혜를 담고 있습니다. 높이 9.17미터의 이 탑은 일년의 날수와 24절기를 상징하며, 하늘과 땅을 잇는 우주론적 의미를 간직하고 있습니다.

천년이 지난 지금도 경주 들판에 우뚝 서서, 별을 관측하던 신라 천문학자들의 열정과 지혜를 우리에게 전하고 있습니다.''';
  }

  /// 설명이 있는지 확인 (동기 방식)
  static bool hasNarrative(String? artifactName) {
    return getNarrativeSync(artifactName) != null;
  }
}
