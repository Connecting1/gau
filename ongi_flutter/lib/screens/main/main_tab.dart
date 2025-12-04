import 'package:flutter/material.dart';
import 'home_screen.dart';
import 'my_infomation_screen.dart';
import 'upload_screen.dart';
import 'search_screen.dart';
import 'event_screen.dart';

class MainTabPage extends StatefulWidget {
  const MainTabPage({super.key});

  @override
  State<MainTabPage> createState() => _MainTabPageState();
}

class _MainTabPageState extends State<MainTabPage> with SingleTickerProviderStateMixin {
  static const int _tabCount = 4;
  static const double _dividerHeight = 1;
  static const double _bottomPadding = 5;
  static const double _tabFontSize = 12;

  static const String _homeTabText = "홈";
  static const String _searchTabText = "검색";
  static const String _uploadTabText = "업로드";
  static const String _myInfoTabText = "내정보";

  TabController? controller;

  @override
  void initState() {
    super.initState();
    controller = TabController(length: _tabCount, vsync: this);
  }

  /// 개별 탭 빌드
  Widget _buildTab(IconData icon, String text) {
    return Tab(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon),
          Text(
            text,
            style: const TextStyle(fontSize: _tabFontSize),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: Column(
        children: [
          Expanded(
            child: TabBarView(
              physics: const NeverScrollableScrollPhysics(),
              controller: controller,
              children: [
                HomeScreenPage(),
                SearchScreenPage(),
                UploadScreenPage(),
                // EventScreenPage(),
                MyInformationScreenPage(),
              ],
            ),
          ),
          const Padding(
            padding: EdgeInsets.only(bottom: _bottomPadding),
            child: Divider(height: _dividerHeight, color: Colors.grey),
          ),
          TabBar(
            controller: controller,
            indicator: const BoxDecoration(),
            labelColor: Colors.white,
            unselectedLabelColor: Colors.grey,
            tabs: [
              _buildTab(Icons.home, _homeTabText),
              _buildTab(Icons.search, _searchTabText),
              _buildTab(Icons.upload, _uploadTabText),
              // Tab(
              //   child: Column(
              //     mainAxisSize: MainAxisSize.min,
              //     children: [
              //       Icon(Icons.edit_square),
              //       Text("이벤트", style: TextStyle(fontSize: 12)),
              //     ],
              //   ),
              // ),
              _buildTab(Icons.person, _myInfoTabText),
            ],
          ),
        ],
      ),
    );
  }

  @override
  void dispose() {
    controller?.dispose();
    super.dispose();
  }
}