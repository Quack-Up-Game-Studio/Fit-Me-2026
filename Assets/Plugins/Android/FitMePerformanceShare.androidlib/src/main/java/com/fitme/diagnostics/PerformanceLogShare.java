package com.fitme.diagnostics;

import android.app.Activity;
import android.content.ClipData;
import android.content.Intent;
import android.net.Uri;

import androidx.core.content.FileProvider;

import java.io.File;

public final class PerformanceLogShare {
    private PerformanceLogShare() { }

    public static boolean share(Activity activity, String filePath) {
        File file = new File(filePath);
        if (activity == null || !file.isFile()) return false;

        try {
            String authority = activity.getPackageName() + ".fitmeperformancefiles";
            Uri uri = FileProvider.getUriForFile(activity, authority, file);
            Intent sendIntent = new Intent(Intent.ACTION_SEND);
            sendIntent.setType("text/csv");
            sendIntent.putExtra(Intent.EXTRA_STREAM, uri);
            sendIntent.setClipData(ClipData.newUri(activity.getContentResolver(), file.getName(), uri));
            sendIntent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
            activity.startActivity(Intent.createChooser(sendIntent, "Share Fit Me performance log"));
            return true;
        } catch (Exception exception) {
            android.util.Log.e("FitMePerformanceShare", "Unable to share performance log", exception);
            return false;
        }
    }
}
