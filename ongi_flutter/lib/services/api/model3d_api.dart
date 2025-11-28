import 'api_service.dart';

class Model3DApi {
  static const String _modelsEndpoint = "/api/model3d/";
  static const String _artifactsPath = "artifacts/";
  static const String _statusParam = "status";
  static const String _defaultStatus = 'completed';

  static const String _errorField = "error";
  static const String _resultsField = "results";
  static const String _artifactField = "artifact";

  static const String _fetchModelsError = "모델 목록을 가져오는 중 오류 발생";
  static const String _fetchDetailError = "모델 상세 정보를 가져오는 중 오류 발생";
  static const String _fetchArtifactModelsError = "유물 관련 모델 목록을 가져오는 중 오류 발생";
  static const String _notFoundError = "404";
  static const String _notFoundText = "Not Found";

  /// 에러 응답 생성
  static Map<String, dynamic> _createErrorResponse(String message, [dynamic error]) {
    final errorMessage = error != null ? "$message: ${error.toString()}" : message;
    return {_errorField: errorMessage};
  }

  /// 응답이 에러인지 확인
  static bool _isErrorResponse(dynamic result) {
    return result is Map && result.containsKey(_errorField);
  }

  /// 404 에러인지 확인
  static bool _isNotFoundError(dynamic result) {
    if (!_isErrorResponse(result)) return false;

    final errorMessage = result[_errorField].toString();
    return errorMessage.contains(_notFoundError) || errorMessage.contains(_notFoundText);
  }

  /// 유물과 일치하는 모델 필터링
  static List<dynamic> _filterModelsByArtifact(List<dynamic> models, String artifactId) {
    return models.where((model) =>
    model is Map &&
        model.containsKey(_artifactField) &&
        model[_artifactField].toString() == artifactId
    ).toList();
  }

  /// 대체 방식으로 유물 모델 조회 (모든 모델을 가져와서 필터링)
  static Future<dynamic> _fetchArtifactModelsFallback(String artifactId) async {
    final allModels = await fetchModels(status: _defaultStatus);

    if (allModels is List) {
      return _filterModelsByArtifact(allModels, artifactId);
    }

    return allModels;
  }

  /// 완료된 3D 모델 목록 가져오기
  static Future<dynamic> fetchModels({String status = _defaultStatus}) async {
    try {
      return await ApiService.sendRequest(
        endpoint: "$_modelsEndpoint?$_statusParam=$status",
        method: "GET",
      );
    } catch (e) {
      return _createErrorResponse(_fetchModelsError, e);
    }
  }

  /// 3D 모델 상세 정보 가져오기
  static Future<dynamic> fetchModelDetail(String modelId) async {
    try {
      return await ApiService.sendRequest(
        endpoint: "$_modelsEndpoint$modelId/",
        method: "GET",
      );
    } catch (e) {
      return _createErrorResponse(_fetchDetailError, e);
    }
  }

  /// 직접적인 유물 모델 조회 시도
  static Future<dynamic> _tryDirectArtifactFetch(String artifactId) async {
    return await ApiService.sendRequest(
      endpoint: "$_modelsEndpoint$_artifactsPath$artifactId/",
      method: "GET",
    );
  }

  /// 유물 모델 조회 결과 처리
  static Future<dynamic> _handleArtifactFetchResult(dynamic result, String artifactId) async {
    if (_isErrorResponse(result)) {
      if (_isNotFoundError(result)) {
        return await _fetchArtifactModelsFallback(artifactId);
      }
      return result;
    }

    return result;
  }

  /// 특정 유물의 3D 모델 목록 가져오기
  static Future<dynamic> fetchArtifactModels(String artifactId) async {
    try {
      final result = await _tryDirectArtifactFetch(artifactId);
      return await _handleArtifactFetchResult(result, artifactId);
    } catch (e) {
      // 예외 발생 시 대체 방식으로 시도
      try {
        return await _fetchArtifactModelsFallback(artifactId);
      } catch (innerError) {
        return _createErrorResponse(_fetchArtifactModelsError, e);
      }
    }
  }
}