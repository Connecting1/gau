# libil2cpp.so 누락 문제 해결 가이드

## 문제 요약

```
JNI FatalError called: Unable to load library: libil2cpp.so [dlopen failed: library "libil2cpp.so" not found]
```

Unity IL2CPP 네이티브 라이브러리가 누락되어 앱이 크래시됩니다.

---

## 해결 방법 1: Unity 완전 빌드 후 Export (권장)

### 단계별 절차

1. **Unity Editor 열기**
   ```bash
   # unity_gaussian_splatting_viewer 프로젝트 열기
   ```

2. **Android 플랫폼 설정 확인**
   - `File` → `Build Settings`
   - `Platform`: **Android** 선택
   - `Switch Platform` 클릭 (아직 Android가 아닌 경우)

3. **Player Settings 확인**
   - `Build Settings` → `Player Settings...` 클릭
   - `Other Settings` 섹션에서:
     - ✅ **Scripting Backend**: `IL2CPP`
     - ✅ **Target Architectures**: `ARM64` 및 `ARMv7` 체크
     - ✅ **Minimum API Level**: `Android 7.0 (API Level 24)`

4. **임시 경로에 APK 빌드 (중요!)**

   이 단계에서 `libil2cpp.so`가 생성됩니다.

   - `Build Settings` 열기
   - **"Export Project" 체크 해제**
   - `Build` 버튼 클릭
   - 임시 폴더 선택 (예: `~/temp_unity_build`)
   - 빌드 완료 대기 (10-20분 소요 가능)

5. **Flutter 프로젝트로 Export**

   - `File` → `Build Settings` 다시 열기
   - **"Export Project" 체크**
   - `Export` 버튼 클릭
   - 경로 선택:
     ```
     /home/user/gausian/ongi_flutter/android/unityLibrary
     ```
   - "Replace existing files" 확인

6. **Export 검증**

   다음 파일들이 존재해야 합니다:
   ```bash
   # 터미널에서 확인
   ls -la ongi_flutter/android/unityLibrary/src/main/jniLibs/arm64-v8a/

   # 다음 파일들이 있어야 함:
   # - libil2cpp.so        ← 이 파일이 가장 중요!
   # - libunity.so
   # - libmain.so
   # - lib_burst_generated.so
   ```

7. **Flutter 앱 빌드 테스트**
   ```bash
   cd ongi_flutter
   flutter clean
   flutter build apk --debug
   ```

---

## 해결 방법 2: Scripting Backend를 Mono로 변경

IL2CPP 대신 Mono 스크립팅 백엔드를 사용하면 `libil2cpp.so`가 필요 없습니다.

### 장점
- 빌드 시간 단축
- 설정이 간단함

### 단점
- 성능이 IL2CPP보다 낮음
- 일부 최신 기능 미지원
- Google Play 64비트 요구사항 충족 어려움

### 절차

1. Unity Editor에서 `File` → `Build Settings` → `Player Settings`
2. `Other Settings` → `Scripting Backend`: **Mono** 선택
3. `File` → `Build Settings`
4. **"Export Project"** 체크
5. `Export` 클릭
6. 경로: `/home/user/gausian/ongi_flutter/android/unityLibrary`

---

## 해결 방법 3: 기존 빌드에서 라이브러리 복사

이미 다른 환경에서 Unity 프로젝트를 성공적으로 빌드한 적이 있다면:

1. **기존 APK/AAB에서 추출**
   ```bash
   # APK를 ZIP으로 이름 변경
   cp app-release.apk app-release.zip

   # 압축 해제
   unzip app-release.zip -d extracted_apk

   # libil2cpp.so 찾기
   find extracted_apk -name "libil2cpp.so"

   # 파일 복사
   cp extracted_apk/lib/arm64-v8a/libil2cpp.so \
      ongi_flutter/android/unityLibrary/src/main/jniLibs/arm64-v8a/

   cp extracted_apk/lib/armeabi-v7a/libil2cpp.so \
      ongi_flutter/android/unityLibrary/src/main/jniLibs/armeabi-v7a/
   ```

2. **Unity Library 빌드 출력에서 복사**
   ```bash
   # Unity에서 빌드한 Gradle 프로젝트가 있다면
   find <unity-build-path> -name "libil2cpp.so"

   # 복사
   cp <unity-build-path>/.../libil2cpp.so \
      ongi_flutter/android/unityLibrary/src/main/jniLibs/arm64-v8a/
   ```

---

## 검증 체크리스트

Export/빌드 후 다음을 확인하세요:

- [ ] `libil2cpp.so` 파일이 존재하는가?
  ```bash
  ls -lh ongi_flutter/android/unityLibrary/src/main/jniLibs/arm64-v8a/libil2cpp.so
  ```

- [ ] 파일 크기가 0보다 큰가? (보통 30-50MB)
  ```bash
  du -h ongi_flutter/android/unityLibrary/src/main/jniLibs/arm64-v8a/libil2cpp.so
  ```

- [ ] 두 아키텍처 모두 존재하는가?
  ```bash
  ls -lh ongi_flutter/android/unityLibrary/src/main/jniLibs/*/libil2cpp.so
  ```

- [ ] Flutter 빌드가 성공하는가?
  ```bash
  cd ongi_flutter
  flutter clean
  flutter build apk --debug
  ```

- [ ] 앱이 크래시 없이 실행되는가?
  ```bash
  flutter run --release
  # 또는
  flutter install
  adb logcat | grep -i "il2cpp"
  ```

---

## 추가 문제 해결

### "NDK not found" 에러

```bash
# Android Studio에서:
# Tools → SDK Manager → SDK Tools → NDK (Side by side) 설치

# 또는 local.properties에 경로 추가:
echo "ndk.dir=/path/to/android-sdk/ndk/25.1.8937393" >> ongi_flutter/android/local.properties
```

### "IL2CPP build failed" 에러

Unity에서 빌드 시:
1. `Edit` → `Preferences` → `External Tools`
2. **NDK 경로**가 올바른지 확인
3. **JDK 경로**가 올바른지 확인

### Export 후에도 libil2cpp.so가 없는 경우

Unity Build Settings에서:
1. `Build System`: **Gradle** 선택
2. `Development Build` **체크 해제**
3. **Clean Build** 수행

---

## 참고 자료

- Unity 공식 문서: [IL2CPP](https://docs.unity3d.com/Manual/IL2CPP.html)
- Unity 포럼: [Android IL2CPP builds](https://forum.unity.com/forums/android.111/)
- Unity Integration Guide: `UNITY_INTEGRATION_GUIDE.md`

---

## 문의

이슈가 계속되면 다음 정보를 포함하여 문의하세요:

```bash
# Unity 버전
# Build Settings 스크린샷
# Player Settings → Other Settings 스크린샷

# 빌드 로그
cat ~/Library/Logs/Unity/Editor.log  # macOS
# 또는
cat ~/.config/unity3d/Editor.log  # Linux

# Unity Export 후 파일 목록
find ongi_flutter/android/unityLibrary/src/main/jniLibs -name "*.so"
```
