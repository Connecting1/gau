# Unity 무한 로딩 문제 해결 완료

## 🔴 문제 상황

Unity 엔진이 "Unity 엔진 초기화 중..." 화면에서 무한 대기 상태에 빠지는 문제가 발생했습니다.

## 🔍 원인 분석

### 1. Unity-Flutter 통신 불완전
Unity가 초기화를 완료했지만 Flutter에 명시적으로 알리지 않아, Flutter는 Unity가 준비되었는지 알 수 없었습니다.

### 2. 타이밍 문제
- Flutter의 `onUnityCreated` 콜백은 Unity 위젯이 생성될 때 호출되지만, Unity의 씬과 컴포넌트가 완전히 초기화되기 전일 수 있음
- `SplatLoader`의 `Start()` 메서드가 실행되기 전에 Flutter에서 모델 로드를 시도할 수 있음

### 3. Android 렌더링 최적화 부족
`useAndroidViewSurface` 파라미터 미설정으로 인한 렌더링 성능 문제 가능성

## ✅ 적용된 해결책

### 1. Unity 측 수정 (`SplatLoader.cs`)

**변경 내용:**
- Unity 초기화 완료 시 `unity_ready` 메시지를 Flutter로 전송
- 1초 대기 후 전송하여 모든 컴포넌트가 완전히 초기화되도록 보장

**추가된 코드:**
```csharp
void Start()
{
    // ... 기존 초기화 코드 ...

    // Unity 초기화 완료를 Flutter에 알림 (1초 후 - Unity 완전 초기화 대기)
    Invoke("NotifyUnityReady", 1.0f);
}

/// <summary>
/// Unity 초기화 완료를 Flutter에 알림
/// </summary>
private void NotifyUnityReady()
{
    Debug.Log("Sending Unity ready notification to Flutter");
    SendMessageToFlutter("unity_ready", "Unity initialization completed");
}
```

### 2. Flutter 측 수정 (`gaussian_splatting_viewer_screen.dart`)

**변경 내용 1: unity_ready 메시지 처리**
- Unity로부터 `unity_ready` 메시지를 받으면 모델 로딩 시작
- 명시적인 초기화 완료 신호로 타이밍 문제 해결

```dart
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
```

**변경 내용 2: Fallback 메커니즘 추가**
- `unity_ready` 메시지가 5초 내에 오지 않으면 자동으로 모델 전송
- 안정성 향상

```dart
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
    if (!_isModelLoaded && provider.currentFilePath != null) {
      debugPrint('Timeout: Unity ready signal not received, sending model anyway');
      _sendModelToUnity(provider.currentFilePath!);
    }
  });
}
```

**변경 내용 3: Android 렌더링 최적화**
```dart
Widget _buildUnityViewer() {
  return UnityWidget(
    onUnityCreated: _onUnityCreated,
    onUnityMessage: _onUnityMessage,
    fullscreen: false,
    hideStatus: true,
    useAndroidViewSurface: true,  // Android에서 더 나은 렌더링 성능
  );
}
```

**변경 내용 4: 디버그 로깅 강화**
- 모든 Unity 메시지에 대한 로깅 추가
- 문제 추적이 용이하도록 개선

## 🚀 다음 단계 (필수!)

이 수정사항들을 반영하려면 **Unity 프로젝트를 다시 빌드하고 Flutter 앱을 재빌드**해야 합니다:

### 1. Unity 프로젝트 Export

Unity Editor가 있는 경우:

```bash
# 1. Unity Editor에서 프로젝트 열기
# - 경로: unity_gaussian_splatting_viewer/UnityGaussianSplattingViewer

# 2. File → Build Settings
# - Platform: Android 선택
# - Switch Platform (필요시)

# 3. Player Settings 확인
# - Other Settings → Scripting Backend: IL2CPP
# - Other Settings → Target Architectures: ARM64, ARMv7 체크

# 4. 먼저 완전한 빌드 수행 (libil2cpp.so 생성)
# - Export Project 체크 해제
# - Build 클릭
# - 임시 폴더 선택 (예: ~/temp_unity_build)
# - 빌드 완료 대기 (10-20분)

# 5. Flutter 프로젝트로 Export
# - Export Project 체크
# - Export 클릭
# - 경로: ongi_flutter/android/unityLibrary
# - Replace existing files 확인

# 6. Export 검증
ls -lh ongi_flutter/android/unityLibrary/src/main/jniLibs/arm64-v8a/libil2cpp.so
# 파일이 존재하고 크기가 30-50MB 정도면 성공
```

### 2. Flutter 앱 빌드

```bash
cd ongi_flutter

# 클린 빌드
flutter clean
flutter pub get

# Debug APK 빌드 (테스트용)
flutter build apk --debug

# 또는 Release APK 빌드 (배포용)
flutter build apk --release

# 앱 설치 및 실행
flutter install
# 또는
adb install -r build/app/outputs/flutter-apk/app-debug.apk
```

### 3. 테스트 및 로그 확인

```bash
# 앱 실행 후 로그 확인
adb logcat -s Unity flutter

# Unity 로그 확인
adb logcat | grep -i "SplatLoader\|Unity ready\|unity_ready"

# Flutter 로그 확인
adb logcat | grep -i "Unity message received\|Unity widget created"
```

## 📊 예상 동작 흐름

수정 후 정상 동작 흐름:

1. **사용자**: "가우시안 스플래팅" 버튼 클릭
2. **Flutter**: GaussianSplattingViewerScreen 열기, 로딩 표시 시작
3. **Flutter**: UnityWidget 생성
4. **Unity**: Unity 엔진 초기화 시작
5. **Flutter**: `onUnityCreated` 콜백 호출, "Unity 엔진 로드 중..." 표시
6. **Unity**: 씬 로드, SplatLoader.Start() 실행
7. **Unity**: 1초 후 `unity_ready` 메시지 전송 ✨ (NEW!)
8. **Flutter**: `unity_ready` 메시지 수신, 모델 파일 경로 전송 ✨ (NEW!)
9. **Unity**: 모델 로딩 시작, `loading_started` 메시지 전송
10. **Flutter**: "모델 로딩 시작..." 표시
11. **Unity**: 모델 로딩 완료, `loading_completed` 메시지 전송
12. **Flutter**: 로딩 오버레이 숨김, 3D 뷰어 표시

## 🔧 트러블슈팅

### 여전히 무한 로딩이 발생하는 경우

**1. Unity 빌드를 다시 했는지 확인**
```bash
# SplatLoader.cs 파일에 NotifyUnityReady() 메서드가 있는지 확인
grep -n "NotifyUnityReady" unity_gaussian_splatting_viewer/*/Assets/Scripts/SplatLoader.cs

# 있어야 함: NotifyUnityReady() 메서드 정의
```

**2. Flutter 앱을 완전히 재빌드했는지 확인**
```bash
cd ongi_flutter
flutter clean
rm -rf build/
flutter pub get
flutter build apk --debug
```

**3. 로그 확인**
```bash
# Unity ready 메시지가 전송되는지 확인
adb logcat | grep "Sending Unity ready notification"

# Flutter가 메시지를 받는지 확인
adb logcat | grep "Unity message received: type=unity_ready"
```

**4. 5초 fallback이 작동하는지 확인**
```bash
# Timeout 메시지가 나오는지 확인
adb logcat | grep "Timeout: Unity ready signal not received"
```

### libil2cpp.so 관련 에러

```bash
# libil2cpp.so 파일 확인
ls -lh ongi_flutter/android/unityLibrary/src/main/jniLibs/arm64-v8a/libil2cpp.so

# 파일이 없거나 너무 작으면 Unity에서 완전한 빌드 후 다시 Export 필요
```

### Unity 씬 설정 확인

Unity Editor에서:
1. `GaussianSplattingViewer.unity` 씬 열기
2. Hierarchy에 다음 GameObject들이 있는지 확인:
   - `SplatLoader` (SplatLoader 컴포넌트 포함)
   - `UnityMessageManager` (UnityMessageManager 컴포넌트 포함)
   - `Main Camera` (OrbitCamera 컴포넌트 포함)

## 📝 수정 파일 목록

### Unity 프로젝트
- `unity_gaussian_splatting_viewer/Assets/Scripts/SplatLoader.cs`
- `unity_gaussian_splatting_viewer/UnityGaussianSplattingViewer/Assets/Scripts/SplatLoader.cs`

### Flutter 프로젝트
- `ongi_flutter/lib/screens/gaussian_splatting/gaussian_splatting_viewer_screen.dart`

## ✅ 검증 체크리스트

빌드 및 배포 전 확인사항:

- [ ] Unity 프로젝트에서 SplatLoader.cs에 NotifyUnityReady() 메서드 추가 확인
- [ ] Unity 프로젝트 완전 빌드 수행 (libil2cpp.so 생성)
- [ ] Unity 프로젝트 Export 완료 (ongi_flutter/android/unityLibrary)
- [ ] libil2cpp.so 파일 존재 및 크기 확인 (30-50MB)
- [ ] Flutter clean 및 재빌드
- [ ] 앱 설치 및 실행
- [ ] 로그에서 "Sending Unity ready notification" 확인
- [ ] 로그에서 "Unity message received: type=unity_ready" 확인
- [ ] "Unity 엔진 초기화 중..." 메시지가 사라지고 3D 뷰어가 표시되는지 확인

## 🎯 요약

이번 수정으로:
1. ✅ Unity 초기화 완료를 명시적으로 Flutter에 알림
2. ✅ Fallback 메커니즘으로 안정성 향상
3. ✅ Android 렌더링 최적화
4. ✅ 디버그 로깅 강화로 문제 추적 용이

**중요**: 코드 수정만으로는 해결되지 않으며, 반드시 Unity 재빌드 및 Flutter 재빌드가 필요합니다!

---

**작성일**: 2025-11-28
**상태**: 코드 수정 완료, 빌드 대기 중
