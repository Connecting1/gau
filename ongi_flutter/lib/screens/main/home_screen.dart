import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../providers/model3d_provider.dart';
import '../../services/api/api_service.dart';
import '../gaussian_splatting/gaussian_splatting_viewer_screen.dart';

class HomeScreenPage extends StatefulWidget {
  @override
  _HomeScreenPageState createState() => _HomeScreenPageState();
}

class _HomeScreenPageState extends State<HomeScreenPage> {
  static const String _headerImagePath = 'assets/images/eaves.png';
  static const String _defaultLoadingMessage = '모델 불러오는 중...';
  static const String _defaultModelName = '유물';
  static const String _incompleteModelMessage = '모델 정보가 불완전합니다';
  static const String _noModelsMessage = '사용 가능한 모델이 없습니다';
  static const String _loadFailedMessage = '모델 로드 실패';
  static const String _loadErrorMessage = '모델을 불러올 수 없습니다';
  static const String _retryButtonText = '다시 시도';
  static const String _gaussianButtonText = '가우시안 스플래팅 보기';

  static const double _titleFontSize = 24;
  static const double _descriptionFontSize = 14;
  static const double _errorIconSize = 48;
  static const int _descriptionMaxLines = 3;

  bool _isLoading = true;
  String _thumbnailUrl = '';
  String _modelName = _defaultLoadingMessage;
  String _modelDescription = '';
  int _currentModelIndex = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _loadModelData());
  }

  /// 모델 데이터 로드
  Future<void> _loadModelData() async {
    setState(() => _isLoading = true);

    try {
      final modelProvider = Provider.of<Model3DProvider>(context, listen: false);
      await modelProvider.fetchCompletedModels();

      if (modelProvider.models.isNotEmpty) {
        _processCurrentModel(modelProvider);
      } else {
        _setErrorState(_noModelsMessage);
      }
    } catch (e) {
      _setErrorState(_loadFailedMessage);
    }
  }

  /// 현재 모델 처리
  void _processCurrentModel(Model3DProvider modelProvider) {
    final currentModel = modelProvider.currentModel;

    if (currentModel != null) {
      _updateModelInfo(currentModel, modelProvider.currentIndex);
    } else {
      _setErrorState(_incompleteModelMessage);
    }
  }

  /// 모델 정보 업데이트
  void _updateModelInfo(dynamic currentModel, int currentIndex) {
    setState(() {
      // 썸네일 URL 우선 사용, 없으면 기본 이미지
      final thumbnailPath = currentModel['thumbnail_url'];
      _thumbnailUrl = thumbnailPath != null ? _buildImageUrl(thumbnailPath.toString()) : '';

      _modelName = currentModel['artifact_name'] ?? _defaultModelName;
      _modelDescription = currentModel['description'] ?? '';
      _currentModelIndex = currentIndex;
      _isLoading = false;
    });
  }

  /// 이미지 URL 구성
  String _buildImageUrl(String imagePath) {
    if (imagePath.startsWith('http://') || imagePath.startsWith('https://')) {
      return imagePath;
    }

    String formattedUrl = imagePath.startsWith('/') ? imagePath : '/$imagePath';
    return "${ApiService.baseUrl}$formattedUrl";
  }

  /// 에러 상태 설정
  void _setErrorState(String message) {
    setState(() {
      _modelName = message;
      _isLoading = false;
    });
  }

  /// 다음 모델로 이동
  void _nextModel() {
    final modelProvider = Provider.of<Model3DProvider>(context, listen: false);
    modelProvider.nextModel();
    _updateCurrentModelInfo();
  }

  /// 이전 모델로 이동
  void _previousModel() {
    final modelProvider = Provider.of<Model3DProvider>(context, listen: false);
    modelProvider.previousModel();
    _updateCurrentModelInfo();
  }

  /// 현재 모델 정보만 업데이트
  void _updateCurrentModelInfo() {
    final modelProvider = Provider.of<Model3DProvider>(context, listen: false);
    final currentModel = modelProvider.currentModel;

    if (currentModel != null) {
      _updateModelInfo(currentModel, modelProvider.currentIndex);
    }
  }

  /// 헤더 이미지
  Widget _buildHeaderImage() {
    return Image.asset(
      _headerImagePath,
      width: double.infinity,
      fit: BoxFit.cover,
    );
  }

  /// 가우시안 스플래팅 뷰어 열기
  void _openGaussianSplattingViewer() {
    final modelProvider = Provider.of<Model3DProvider>(context, listen: false);
    final currentModel = modelProvider.currentModel;

    if (currentModel == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('표시할 모델이 없습니다'),
          backgroundColor: Colors.orange,
        ),
      );
      return;
    }

    // 가우시안 스플래팅 모델 URL 확인
    String? gaussianModelUrl = currentModel['gaussian_model_url'];

    if (gaussianModelUrl == null || gaussianModelUrl.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('가우시안 스플래팅 모델이 없습니다'),
          backgroundColor: Colors.orange,
        ),
      );
      return;
    }

    // 뷰어 화면으로 이동
    Navigator.push(
      context,
      MaterialPageRoute(
        builder: (context) => GaussianSplattingViewerScreen(
          modelId: currentModel['id']?.toString() ?? 'unknown',
          modelUrl: gaussianModelUrl,
          modelName: currentModel['artifact_name'] ?? '유물',
          description: currentModel['description'],
        ),
      ),
    );
  }

  /// 모델 타이틀과 네비게이션
  Widget _buildModelHeader(Model3DProvider modelProvider) {
    return Padding(
      padding: const EdgeInsets.all(16.0),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          _buildPreviousButton(modelProvider),
          _buildModelTitle(),
          _buildNextButton(modelProvider),
        ],
      ),
    );
  }

  /// 이전 버튼
  Widget _buildPreviousButton(Model3DProvider modelProvider) {
    if (modelProvider.models.length <= 1) {
      return const SizedBox.shrink();
    }

    return IconButton(
      icon: const Icon(Icons.arrow_back_ios, color: Colors.white),
      onPressed: _previousModel,
    );
  }

  /// 모델 타이틀
  Widget _buildModelTitle() {
    return Expanded(
      child: Text(
        _modelName,
        style: const TextStyle(
          fontSize: _titleFontSize,
          fontWeight: FontWeight.bold,
          color: Colors.white,
        ),
        textAlign: TextAlign.center,
      ),
    );
  }

  /// 다음 버튼
  Widget _buildNextButton(Model3DProvider modelProvider) {
    if (modelProvider.models.length <= 1) {
      return const SizedBox.shrink();
    }

    return IconButton(
      icon: const Icon(Icons.arrow_forward_ios, color: Colors.white),
      onPressed: _nextModel,
    );
  }

  /// 모델 인덱스 표시
  Widget _buildModelCounter(Model3DProvider modelProvider) {
    if (modelProvider.models.length <= 1) {
      return const SizedBox.shrink();
    }

    return Padding(
      padding: const EdgeInsets.only(bottom: 8.0),
      child: Text(
        '${_currentModelIndex + 1} / ${modelProvider.models.length}',
        style: const TextStyle(color: Colors.white),
      ),
    );
  }

  /// 썸네일 이미지 표시
  Widget _buildThumbnailImage() {
    if (_isLoading) {
      return const Center(child: CircularProgressIndicator());
    }

    if (_thumbnailUrl.isEmpty) {
      return _buildErrorView();
    }

    return Container(
      color: Colors.black,
      child: Center(
        child: Image.network(
          _thumbnailUrl,
          fit: BoxFit.contain,
          loadingBuilder: (context, child, loadingProgress) {
            if (loadingProgress == null) return child;
            return Center(
              child: CircularProgressIndicator(
                value: loadingProgress.expectedTotalBytes != null
                    ? loadingProgress.cumulativeBytesLoaded /
                        loadingProgress.expectedTotalBytes!
                    : null,
              ),
            );
          },
          errorBuilder: (context, error, stackTrace) {
            return const Icon(
              Icons.image_not_supported,
              size: 64,
              color: Colors.grey,
            );
          },
        ),
      ),
    );
  }

  /// 에러 뷰
  Widget _buildErrorView() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(
            Icons.error_outline,
            size: _errorIconSize,
            color: Colors.grey,
          ),
          const SizedBox(height: 16),
          const Text(
            _loadErrorMessage,
            style: TextStyle(color: Colors.grey),
          ),
          const SizedBox(height: 8),
          ElevatedButton(
            onPressed: _loadModelData,
            child: const Text(_retryButtonText),
          ),
        ],
      ),
    );
  }

  /// 가우시안 스플래팅 버튼
  Widget _buildGaussianButton() {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16.0, vertical: 8.0),
      child: SizedBox(
        width: double.infinity,
        height: 56,
        child: ElevatedButton.icon(
          onPressed: _openGaussianSplattingViewer,
          icon: const Icon(Icons.view_in_ar, size: 24),
          label: const Text(
            _gaussianButtonText,
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.bold,
            ),
          ),
          style: ElevatedButton.styleFrom(
            backgroundColor: Colors.blue,
            foregroundColor: Colors.white,
            elevation: 4,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(12),
            ),
          ),
        ),
      ),
    );
  }

  /// 모델 설명
  Widget _buildModelDescription() {
    if (_modelDescription.isEmpty || _isLoading) {
      return const SizedBox.shrink();
    }

    return Padding(
      padding: const EdgeInsets.all(16.0),
      child: Text(
        _modelDescription,
        style: const TextStyle(
          fontSize: _descriptionFontSize,
          color: Colors.white,
        ),
        maxLines: _descriptionMaxLines,
        overflow: TextOverflow.ellipsis,
      ),
    );
  }

  /// 모델 뷰어 섹션
  Widget _buildModelViewerSection(Model3DProvider modelProvider) {
    return Expanded(
      child: Column(
        children: [
          _buildModelHeader(modelProvider),
          _buildModelCounter(modelProvider),
          Expanded(child: _buildThumbnailImage()),
          _buildGaussianButton(),
          _buildModelDescription(),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final modelProvider = Provider.of<Model3DProvider>(context);

    return Scaffold(
      backgroundColor: Colors.black,
      body: Column(
        children: [
          _buildHeaderImage(),
          _buildModelViewerSection(modelProvider),
        ],
      ),
    );
  }
}
