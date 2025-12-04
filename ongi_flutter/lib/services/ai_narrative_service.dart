import 'dart:convert';
import 'package:http/http.dart' as http;
import 'api/api_service.dart';

/// AI 서술형 설명 생성 서비스
/// 백엔드 API를 통해 유물 이름 기반으로 서술형 설명을 자동 생성합니다.
class AiNarrativeService {
  /// 캐시된 설명 저장소 (메모리 캐시)
  static final Map<String, String> _narrativeCache = {};

  /// AI 서술형 설명 생성 (Ollama llama3.1:8b)
  ///
  /// [artifactName] 유물 이름
  /// [useCache] 캐시 사용 여부 (기본값: true)
  /// [useAi] Ollama AI 사용 여부 (기본값: true)
  ///
  /// 반환값: AI 생성된 서술형 설명 또는 null
  static Future<String?> generateNarrative(
    String artifactName, {
    bool useCache = true,
    bool useAi = true,
  }) async {
    if (artifactName.isEmpty) return null;

    // 캐시 확인
    if (useCache && _narrativeCache.containsKey(artifactName)) {
      return _narrativeCache[artifactName];
    }

    try {
      // API 요청
      final url = Uri.parse('${ApiService.baseUrl}/api/artifacts/generate-narrative/');
      final response = await http.post(
        url,
        headers: {
          'Content-Type': 'application/json',
        },
        body: jsonEncode({
          'artifact_name': artifactName,
          'use_ai': useAi,
        }),
      );

      if (response.statusCode == 200) {
        final data = jsonDecode(utf8.decode(response.bodyBytes));
        final narrative = data['narrative'] as String?;

        // 캐시에 저장
        if (narrative != null && narrative.isNotEmpty) {
          _narrativeCache[artifactName] = narrative;
          return narrative;
        }
      }

      return null;
    } catch (e) {
      print('Error generating AI narrative: $e');
      return null;
    }
  }

  /// 캐시 클리어
  static void clearCache() {
    _narrativeCache.clear();
  }

  /// 특정 유물의 캐시 삭제
  static void removeCacheFor(String artifactName) {
    _narrativeCache.remove(artifactName);
  }
}
