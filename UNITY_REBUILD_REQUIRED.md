# Unity 초기화 무한 대기 문제 - 해결 가이드

## 🔴 문제 상황

Unity 엔진이 "Unity 엔진 초기화 중..." 화면에서 무한 대기 중입니다.

### 근본 원인

**필수 네이티브 라이브러리 `libil2cpp.so` 파일이 누락되었습니다.**

```bash
# 현재 상태
❌ libil2cpp.so - MISSING (30-50MB)
✅ libunity.so - EXISTS (13MB)
✅ libmain.so - EXISTS (6.6KB)
✅ lib_burst_generated.so - EXISTS (5.1KB)
```

Unity 프로젝트가 IL2CPP 스크립팅 백엔드로 설정되어 있어 이 파일 없이는 Unity가 초기화될 수 없습니다.

---

## ✅ 해결 방법: Unity Editor에서 올바른 빌드 후 Export

### 전제 조건
- Unity Editor 2022.3.x 설치됨
- Android Build Support 모듈 설치됨
- Android NDK 설치됨

### 단계별 가이드

#### 1. Unity 프로젝트 열기

```bash
# Unity Editor에서 다음 프로젝트 열기
/home/user/gausian/unity_gaussian_splatting_viewer/UnityGaussianSplattingViewer
```

#### 2. Android 플랫폼 설정 확인

1. **File → Build Settings** 열기
2. **Platform: Android** 선택
3. **Switch Platform** 클릭 (아직 Android가 아닌 경우)

#### 3. Player Settings 확인

**Build Settings → Player Settings** 클릭 후:

**Other Settings** 섹션에서:
- ✅ **Scripting Backend**: `IL2CPP` (현재 설정 유지)
- ✅ **Target Architectures**:
  - ☑ ARM64
  - ☑ ARMv7
- ✅ **API Level**:
  - Minimum API Level: `Android 7.0 (API Level 24)`
  - Target API Level: `Automatic (highest installed)`

**Publishing Settings** 섹션에서:
- Package Name: `com.example.ongi_flutter` (Flutter와 동일하게)

#### 4. ⚠️ 중요: 먼저 완전한 APK 빌드 수행

**이 단계가 libil2cpp.so를 생성합니다!**

1. **File → Build Settings** 열기
2. **"Export Project" 체크 해제** ❌
3. **Build** 버튼 클릭
4. 임시 폴더 선택 (예: `~/temp_unity_build/`)
5. **빌드 완료 대기** (10-20분 소요)

빌드가 성공하면 libil2cpp.so가 생성됩니다.

#### 5. Flutter 프로젝트로 Export

이제 libil2cpp.so가 포함된 상태로 export합니다:

1. **File → Build Settings** 다시 열기
2. **"Export Project" 체크** ✅
3. **Export** 버튼 클릭
4. Export 경로 입력:
   ```
   /home/user/gausian/ongi_flutter/android/unityLibrary
   ```
5. **"Replace existing files" 확인** ✅
6. Export 완료 대기

#### 6. 검증

Export 후 다음 명령어로 검증:

```bash
# libil2cpp.so 존재 확인
ls -lh /home/user/gausian/ongi_flutter/android/unityLibrary/src/main/jniLibs/arm64-v8a/libil2cpp.so

# 파일 크기 확인 (30-50MB여야 함)
du -h /home/user/gausian/ongi_flutter/android/unityLibrary/src/main/jniLibs/arm64-v8a/libil2cpp.so

# 두 아키텍처 모두 확인
ls -lh /home/user/gausian/ongi_flutter/android/unityLibrary/src/main/jniLibs/*/libil2cpp.so
```

#### 7. Flutter 앱 빌드 및 테스트

```bash
cd /home/user/gausian/ongi_flutter
flutter clean
flutter build apk --debug
flutter install
```

---

## 🔄 대안: Mono 스크립팅 백엔드 사용

IL2CPP 대신 Mono를 사용하면 libil2cpp.so가 필요 없습니다.

### 장점
- 빌드 시간 단축
- libil2cpp.so 불필요
- 설정이 간단

### 단점
- 성능이 IL2CPP보다 낮음
- Google Play 64비트 요구사항 충족 어려움

### 절차

1. Unity Editor에서 **File → Build Settings → Player Settings**
2. **Other Settings → Scripting Backend**: **Mono** 선택
3. **File → Build Settings**
4. **"Export Project"** 체크 ✅
5. **Export** 클릭
6. 경로: `/home/user/gausian/ongi_flutter/android/unityLibrary`

---

## 🚨 현재 상황에서 할 수 없는 것

다음은 **Unity Editor 없이는 불가능**합니다:

❌ Linux에서 IL2CPP 라이브러리 빌드 (il2cpp.exe는 Windows 전용)
❌ Gradle task로 libil2cpp.so 생성 (il2cpp 컴파일러 필요)
❌ Scripting backend 변경 (Unity Editor 필요)

---

## 📋 체크리스트

Export 후 확인사항:

- [ ] `libil2cpp.so` 파일이 `jniLibs/arm64-v8a/`에 존재
- [ ] `libil2cpp.so` 파일이 `jniLibs/armeabi-v7a/`에 존재
- [ ] 각 파일 크기가 30-50MB
- [ ] Flutter 빌드가 성공
- [ ] 앱이 Unity 초기화 완료

---

## 🎯 빠른 해결 (Unity Editor 접근 가능한 경우)

```bash
# 1. Unity Editor에서 프로젝트 열기
# 2. Build Settings → Platform: Android
# 3. Player Settings 확인 (IL2CPP, ARM64+ARMv7)
# 4. 먼저 Build (Export 체크 해제) → 임시 폴더
# 5. 다음 Export (Export 체크) → ongi_flutter/android/unityLibrary
# 6. 검증
ls -lh ongi_flutter/android/unityLibrary/src/main/jniLibs/arm64-v8a/libil2cpp.so

# 7. Flutter 빌드
cd ongi_flutter && flutter clean && flutter build apk
```

---

## 💬 추가 도움이 필요한 경우

다음 정보를 제공해주세요:

1. Unity Editor 접근 가능 여부
2. 사용 중인 Unity 버전
3. 개발 환경 (Windows/Mac/Linux)
4. 이전에 성공적으로 빌드한 APK 보유 여부 (libil2cpp.so 추출 가능)

---

**마지막 업데이트**: 2025-11-28
**상태**: 액션 필요 - Unity Editor에서 rebuild 필요
