import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:flutter_unity_widget/flutter_unity_widget.dart';
import 'dart:convert';
import '../../providers/gaussian_splatting_provider.dart';

/// 가우시안 스플래팅 뷰어 화면
/// Unity 뷰어를 임베드하여 .ply 파일을 렌더링합니다.
class GaussianSplattingViewerScreen extends StatefulWidget {
  final String modelId;
  final String modelUrl;
  final String? modelName;
  final String? description;

  const GaussianSplattingViewerScreen({
    Key? key,
    required this.modelId,
    required this.modelUrl,
    this.modelName,
    this.description,
  }) : super(key: key);

  @override
  _GaussianSplattingViewerScreenState createState() =>
      _GaussianSplattingViewerScreenState();
}

class _GaussianSplattingViewerScreenState
    extends State<GaussianSplattingViewerScreen> {
  static const String _headerImagePath = 'assets/images/eaves.png';
  static const String _preparingMessage = '모델 준비 중...';
  static const String _downloadingMessage = '모델 다운로드 중...';
  static const String _loadingUnityMessage = 'Unity 엔진 초기화 중...';
  static const String _viewerReadyMessage = '뷰어 준비 완료';
  static const String _errorTitle = '오류 발생';
  static const String _retryButton = '다시 시도';
  static const String _cancelButton = '취소';

  UnityWidgetController? _unityController;
  bool _isUnityLoaded = false;
  bool _isModelLoaded = false;
  String _statusMessage = _preparingMessage;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _prepareAndLoadModel();
    });
  }

  /// 모델 준비 및 로드
  Future<void> _prepareAndLoadModel() async {
    final provider = Provider.of<GaussianSplattingProvider>(
      context,
      listen: false,
    );

    try {
      setState(() => _statusMessage = _preparingMessage);

      // 모델 파일 준비 (다운로드 또는 기존 파일 사용)
      final filePath = await provider.prepareModel(
        modelUrl: widget.modelUrl,
        modelId: widget.modelId,
      );

      if (filePath == null) {
        throw Exception('모델 파일 준비 실패');
      }

      // Unity가 로드되었으면 모델 전송
      if (_isUnityLoaded && _unityController != null) {
        _sendModelToUnity(filePath);
      } else {
        // Unity 로드 대기
        setState(() => _statusMessage = _loadingUnityMessage);
      }
    } catch (e) {
      provider.setViewerError('모델 준비 중 오류: ${e.toString()}');
    }
  }

  /// Unity에 모델 파일 경로 전송
  void _sendModelToUnity(String filePath) {
    if (_unityController == null) return;

    final message = jsonEncode({
      'type': 'load_model',
      'data': filePath,
    });

    _unityController!.postMessage(
      'UnityMessageManager',
      'OnFlutterMessage',
      message,
    );

    setState(() {
      _statusMessage = '모델 로딩 중...';
      _isModelLoaded = true;
    });
  }

  /// Unity로부터 메시지 수신
  void _onUnityMessage(dynamic message) {
    if (message == null) return;

    try {
      final Map<String, dynamic> data = jsonDecode(message.toString());
      final type = data['type'];
      final msg = data['message'] ?? '';

      debugPrint('Unity message received: type=$type, message=$msg');

      switch (type) {
        case 'unity_ready':
          // Unity 초기화 완료
          debugPrint('Unity is ready, sending model path...');
          final provider = Provider.of<GaussianSplattingProvider>(
            context,
            listen: false,
          );
          if (provider.currentFilePath != null) {
            _sendModelToUnity(provider.currentFilePath!);
          }
          break;

        case 'loading_started':
          setState(() => _statusMessage = '모델 로딩 시작...');
          break;

        case 'loading_completed':
          setState(() {
            _statusMessage = _viewerReadyMessage;
          });
          Provider.of<GaussianSplattingProvider>(context, listen: false)
              .setViewerReady(true);
          break;

        case 'model_unloaded':
          setState(() {
            _isModelLoaded = false;
            _statusMessage = _preparingMessage;
          });
          break;

        case 'error':
          Provider.of<GaussianSplattingProvider>(context, listen: false)
              .setViewerError(msg);
          break;

        default:
          debugPrint('Unknown Unity message type: $type');
      }
    } catch (e) {
      debugPrint('Failed to parse Unity message: $e');
    }
  }

  /// Unity 생성 완료
  void _onUnityCreated(UnityWidgetController controller) {
    debugPrint('Unity widget created, controller initialized');
    _unityController = controller;
    setState(() {
      _isUnityLoaded = true;
      _statusMessage = 'Unity 엔진 로드 중...';
    });

    // Unity가 unity_ready 메시지를 보낼 때까지 대기
    // 만약 5초 내에 unity_ready 메시지가 없으면 직접 모델 전송 (fallback)
    Future.delayed(const Duration(seconds: 5), () {
      final provider = Provider.of<GaussianSplattingProvider>(
        context,
        listen: false,
      );

      if (!_isModelLoaded && provider.currentFilePath != null) {
        debugPrint('Timeout: Unity ready signal not received, sending model anyway');
        _sendModelToUnity(provider.currentFilePath!);
      }
    });
  }

  /// 헤더 빌드
  Widget _buildHeader() {
    return Stack(
      children: [
        Image.asset(
          _headerImagePath,
          width: double.infinity,
          height: 120,
          fit: BoxFit.cover,
        ),
        SafeArea(
          child: Row(
            children: [
              IconButton(
                icon: const Icon(Icons.arrow_back, color: Colors.white),
                onPressed: () => Navigator.pop(context),
              ),
              Expanded(
                child: Text(
                  widget.modelName ?? '가우시안 스플래팅 뷰어',
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 18,
                    fontWeight: FontWeight.bold,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
              ),
              // 카메라 리셋 버튼
              if (_isModelLoaded)
                IconButton(
                  icon: const Icon(Icons.refresh, color: Colors.white),
                  onPressed: _resetCamera,
                  tooltip: '카메라 리셋',
                ),
            ],
          ),
        ),
      ],
    );
  }

  /// 카메라 리셋
  void _resetCamera() {
    if (_unityController == null) return;

    final message = jsonEncode({
      'type': 'reset_camera',
      'data': '',
    });

    _unityController!.postMessage(
      'UnityMessageManager',
      'OnFlutterMessage',
      message,
    );
  }

  /// Unity 뷰어 빌드
  Widget _buildUnityViewer() {
    return UnityWidget(
      onUnityCreated: _onUnityCreated,
      onUnityMessage: _onUnityMessage,
      fullscreen: false,
      hideStatus: true,
      useAndroidViewSurface: true,  // Android에서 더 나은 렌더링 성능
    );
  }

  /// 로딩 오버레이
  Widget _buildLoadingOverlay(GaussianSplattingProvider provider) {
    // Unity 로드 완료 & 모델 로드 완료 시 오버레이 숨김
    if (_isUnityLoaded && _isModelLoaded && provider.isViewerReady) {
      return const SizedBox.shrink();
    }

    return Container(
      color: Colors.black.withOpacity(0.8),
      child: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            if (provider.isDownloading) ...[
              CircularProgressIndicator(
                value: provider.downloadProgress,
                backgroundColor: Colors.grey.shade700,
                valueColor: const AlwaysStoppedAnimation<Color>(Colors.blue),
              ),
              const SizedBox(height: 24),
              Text(
                _downloadingMessage,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 16,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                '${provider.downloadProgressPercent}%',
                style: const TextStyle(
                  color: Colors.white70,
                  fontSize: 14,
                ),
              ),
            ] else ...[
              const CircularProgressIndicator(),
              const SizedBox(height: 24),
              Text(
                _statusMessage,
                style: const TextStyle(
                  color: Colors.white,
                  fontSize: 16,
                ),
                textAlign: TextAlign.center,
              ),
            ],
          ],
        ),
      ),
    );
  }

  /// 에러 화면
  Widget _buildErrorScreen(GaussianSplattingProvider provider) {
    return Container(
      color: Colors.black,
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(24.0),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const Icon(
                Icons.error_outline,
                color: Colors.red,
                size: 64,
              ),
              const SizedBox(height: 24),
              const Text(
                _errorTitle,
                style: TextStyle(
                  color: Colors.white,
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                ),
              ),
              const SizedBox(height: 16),
              Text(
                provider.errorMessage,
                style: const TextStyle(
                  color: Colors.white70,
                  fontSize: 14,
                ),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 32),
              Row(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  ElevatedButton.icon(
                    onPressed: () => Navigator.pop(context),
                    icon: const Icon(Icons.close),
                    label: const Text(_cancelButton),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.grey.shade700,
                    ),
                  ),
                  const SizedBox(width: 16),
                  ElevatedButton.icon(
                    onPressed: () {
                      provider.clearViewerError();
                      _prepareAndLoadModel();
                    },
                    icon: const Icon(Icons.refresh),
                    label: const Text(_retryButton),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.blue,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  /// 설명 섹션
  Widget _buildDescription() {
    if (widget.description == null || widget.description!.isEmpty) {
      return const SizedBox.shrink();
    }

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(16),
      color: Colors.black.withOpacity(0.7),
      child: Text(
        widget.description!,
        style: const TextStyle(
          color: Colors.white,
          fontSize: 14,
        ),
        maxLines: 3,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: Consumer<GaussianSplattingProvider>(
        builder: (context, provider, child) {
          // 에러 발생 시 에러 화면
          if (provider.hasError) {
            return Column(
              children: [
                _buildHeader(),
                Expanded(child: _buildErrorScreen(provider)),
              ],
            );
          }

          // 정상 화면
          return Column(
            children: [
              _buildHeader(),
              Expanded(
                child: Stack(
                  children: [
                    // Unity 뷰어
                    _buildUnityViewer(),
                    // 로딩 오버레이
                    _buildLoadingOverlay(provider),
                  ],
                ),
              ),
              // 설명 섹션
              _buildDescription(),
            ],
          );
        },
      ),
    );
  }

  @override
  void dispose() {
    // Unity 컨트롤러 정리
    _unityController?.dispose();

    // Provider 상태 초기화
    Provider.of<GaussianSplattingProvider>(context, listen: false)
        .clearCurrentModel();

    super.dispose();
  }
}
