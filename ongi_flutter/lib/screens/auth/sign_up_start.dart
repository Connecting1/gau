import 'package:flutter/material.dart';
import 'package:google_sign_in/google_sign_in.dart';
import 'package:provider/provider.dart';
import '../../providers/sign_up_provider.dart';
import '../../styles/app_styles.dart';
import 'sign_up_email.dart';
import 'sign_up_birth_date.dart';
import 'sign_in.dart';

class SignUpStartPage extends StatefulWidget {
  const SignUpStartPage({Key? key}) : super(key: key);

  @override
  State<SignUpStartPage> createState() => _SignUpStartPageState();
}

class _SignUpStartPageState extends State<SignUpStartPage> {
  static const String _backgroundImagePath = "assets/signup_background.jpg";
  static const String _googleLogoPath = 'assets/logos/google_logo.png';
  static const String _appTitle = '온\n기';
  static const String _loginButtonText = '로그인';
  static const String _googleLoginButtonText = '구글 아이디 로그인';
  static const String _signUpButtonText = '회원가입';
  static const String _localProvider = "local";
  static const String _googleProvider = "google";
  static const String _fontFamily = 'UnPenheulim';

  static const double _titleTopRatio = 0.17;
  static const double _titleRightRatio = 0.1;
  static const double _titleFontSize = 80;
  static const double _titleLineHeight = 1.2;
  static const double _buttonFontSize = 18;
  static const double _signUpFontSize = 15;
  static const double _logoSize = 24;
  static const double _bottomPadding = 50;
  static const double _buttonSpacing = 15;
  static const double _finalBottomSpacing = 30;
  static const double _decorationThickness = 2;

  final GoogleSignIn _googleSignIn = GoogleSignIn(scopes: ['email']);
  GoogleSignInAccount? _currentUser;

  /// 일반 회원가입 처리
  Future<void> _handleSignIn() async {
    final signUpProvider = Provider.of<SignUpProvider>(context, listen: false);
    signUpProvider.setProvider(_localProvider);

    Navigator.push(
      context,
      MaterialPageRoute(builder: (context) => const SignUpEmailPage()),
    );
  }

  /// 로그인 페이지로 이동
  Future<void> _handleLogIn() async {
    Navigator.push(
      context,
      MaterialPageRoute(builder: (context) => const SignInPage()),
    );
  }

  /// Provider에 구글 사용자 정보 저장
  void _saveGoogleUserToProvider(GoogleSignInAccount user) {
    final signUpProvider = Provider.of<SignUpProvider>(context, listen: false);
    signUpProvider.setEmail(user.email);
    signUpProvider.setProvider(_googleProvider);
    signUpProvider.setPassword(null);
  }

  /// 구글 로그인 성공 후 페이지 이동
  void _navigateAfterGoogleSignIn() {
    Navigator.push(
      context,
      MaterialPageRoute(builder: (context) => const SignUpBirthDatePage()),
    );
  }

  /// 구글 로그인 처리
  Future<void> _handleGoogleSignIn() async {
    try {
      await _googleSignIn.signOut();
      final user = await _googleSignIn.signIn();

      if (user != null) {
        setState(() => _currentUser = user);
        _saveGoogleUserToProvider(user);
        _navigateAfterGoogleSignIn();
      }
    } catch (error) {
      // 에러 처리는 필요시 추가
    }
  }

  /// 구글 로그아웃 처리
  Future<void> _handleSignOut() async {
    await _googleSignIn.signOut();
    setState(() => _currentUser = null);
  }

  /// 배경 이미지
  Widget _buildBackground() {
    return Positioned.fill(
      child: Container(
        width: MediaQuery.of(context).size.width,
        height: MediaQuery.of(context).size.height,
        decoration: const BoxDecoration(
          image: DecorationImage(
            image: AssetImage(_backgroundImagePath),
            fit: BoxFit.cover,
          ),
        ),
      ),
    );
  }

  /// 앱 타이틀
  Widget _buildAppTitle() {
    return Positioned(
      top: MediaQuery.of(context).size.height * _titleTopRatio,
      right: MediaQuery.of(context).size.width * _titleRightRatio,
      child: const Text(
        _appTitle,
        style: TextStyle(
          fontSize: _titleFontSize,
          fontWeight: FontWeight.bold,
          color: Colors.white,
          fontFamily: _fontFamily,
          height: _titleLineHeight,
        ),
        textAlign: TextAlign.center,
      ),
    );
  }

  /// 로그인 버튼
  Widget _buildLoginButton() {
    return ElevatedButton.icon(
      onPressed: _handleLogIn,
      label: const Text(
        _loginButtonText,
        style: TextStyle(
          fontSize: _buttonFontSize,
          fontWeight: FontWeight.bold,
        ),
      ),
      style: ButtonStyles.loginButtonStyle(context),
    );
  }

  /// 구글 로그인 버튼
  Widget _buildGoogleLoginButton() {
    return ElevatedButton(
      onPressed: _handleGoogleSignIn,
      style: ButtonStyles.loginButtonStyle(context),
      child: Stack(
        alignment: Alignment.center,
        children: [
          Row(
            mainAxisSize: MainAxisSize.max,
            children: [
              SizedBox(
                width: _logoSize,
                height: _logoSize,
                child: Image.asset(
                  _googleLogoPath,
                  fit: BoxFit.cover,
                ),
              ),
            ],
          ),
          const Text(
            _googleLoginButtonText,
            style: TextStyle(
              fontSize: _buttonFontSize,
              fontWeight: FontWeight.bold,
            ),
            textAlign: TextAlign.center,
          ),
        ],
      ),
    );
  }

  /// 회원가입 텍스트 버튼
  Widget _buildSignUpTextButton() {
    return GestureDetector(
      onTap: _handleSignIn,
      child: const Text(
        _signUpButtonText,
        style: TextStyle(
          fontSize: _signUpFontSize,
          fontWeight: FontWeight.bold,
          color: Colors.white,
          decoration: TextDecoration.underline,
          decorationColor: Colors.white,
          decorationThickness: _decorationThickness,
        ),
      ),
    );
  }

  /// 버튼 그룹
  Widget _buildButtonGroup() {
    return Positioned(
      bottom: _bottomPadding,
      left: 0,
      right: 0,
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          _buildLoginButton(),
          const SizedBox(height: _buttonSpacing),
          _buildGoogleLoginButton(),
          const SizedBox(height: _buttonSpacing),
          _buildSignUpTextButton(),
          const SizedBox(height: _finalBottomSpacing),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Stack(
        children: [
          _buildBackground(),
          _buildAppTitle(),
          _buildButtonGroup(),
        ],
      ),
    );
  }
}