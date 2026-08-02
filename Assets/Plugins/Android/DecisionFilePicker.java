package com.decisiondisc.filepicker;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import com.unity3d.player.UnityPlayer;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;

public class DecisionFilePicker extends Activity {
    private static final int REQUEST = 7193;
    private static String receiver;
    private static String pendingText;
    private static String pendingFileName;
    private static String pendingMimeType;
    private String mode;

    public static void pickText(Activity activity, String gameObject) {
        launch(activity, gameObject, "text", null);
    }

    public static void pickImage(Activity activity, String gameObject) {
        launch(activity, gameObject, "image", null);
    }

    public static void createText(Activity activity, String gameObject, String text, String fileName, String mimeType) {
        pendingFileName = fileName;
        pendingMimeType = mimeType;
        launch(activity, gameObject, "export", text);
    }

    private static void launch(Activity activity, String gameObject, String kind, String text) {
        receiver = gameObject;
        pendingText = text;
        Intent helper = new Intent(activity, DecisionFilePicker.class);
        helper.putExtra("mode", kind);
        activity.startActivity(helper);
    }

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        mode = getIntent().getStringExtra("mode");
        Intent intent;
        if ("export".equals(mode)) {
            intent = new Intent(Intent.ACTION_CREATE_DOCUMENT);
            intent.setType(pendingMimeType == null ? "text/plain" : pendingMimeType);
            intent.putExtra(Intent.EXTRA_TITLE, pendingFileName == null ? "YesNoFilp-export.txt" : pendingFileName);
        } else {
            intent = new Intent(Intent.ACTION_OPEN_DOCUMENT);
            intent.addCategory(Intent.CATEGORY_OPENABLE);
            intent.setType("image".equals(mode) ? "image/*" : "application/json");
        }
        startActivityForResult(intent, REQUEST);
    }

    @Override protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST || resultCode != RESULT_OK || data == null || data.getData() == null) {
            send("OnFilePickerError", "Cancelled"); finish(); return;
        }
        try {
            Uri uri = data.getData();
            if ("export".equals(mode)) {
                try (OutputStream output = getContentResolver().openOutputStream(uri)) {
                    output.write(pendingText.getBytes(StandardCharsets.UTF_8));
                }
            } else if ("text".equals(mode)) {
                try (InputStream input = getContentResolver().openInputStream(uri);
                     ByteArrayOutputStream output = new ByteArrayOutputStream()) {
                    byte[] buffer = new byte[8192]; int count;
                    while ((count = input.read(buffer)) >= 0) output.write(buffer, 0, count);
                    send("OnTextPicked", output.toString("UTF-8"));
                }
            } else {
                File destination = new File(getCacheDir(), "picked-" + System.nanoTime() + ".img");
                try (InputStream input = getContentResolver().openInputStream(uri);
                     OutputStream output = new FileOutputStream(destination)) {
                    byte[] buffer = new byte[8192]; int count;
                    while ((count = input.read(buffer)) >= 0) output.write(buffer, 0, count);
                }
                send("OnImagePicked", destination.getAbsolutePath());
            }
        } catch (Exception error) { send("OnFilePickerError", error.getMessage()); }
        pendingText = null;
        pendingFileName = null;
        pendingMimeType = null;
        finish();
    }

    private static void send(String method, String value) {
        UnityPlayer.UnitySendMessage(receiver, method, value == null ? "" : value);
    }
}
