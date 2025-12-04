package com.example.ongi_flutter

import io.flutter.embedding.android.FlutterActivity

import com.xraph.plugin.flutter_unity_widget.UnityPlayerUtils

class MainActivity: FlutterActivity() {
    fun onUnityMessage(message: String) {
        
        UnityPlayerUtils.onUnityMessage(message)
    }
}