package com.yeonsik.midtermar;

import android.content.Context;
import android.hardware.Sensor;
import android.hardware.SensorEvent;
import android.hardware.SensorEventListener;
import android.hardware.SensorManager;
import android.os.Handler;
import android.os.Looper;

/** Keeps an explicit motion-sensor request while AR is active; never consumes pose data. */
public final class MotionSampling implements SensorEventListener {
    private static final int SAMPLING_PERIOD_US = 10000; // AR 사용 중 요청할 센서 주기 10ms
    private final SensorManager manager; // 앱 Context로 얻은 Android 센서 서비스
    private final Handler callbackHandler = new Handler(Looper.getMainLooper()); // 빈 센서 콜백을 Android 메인 스레드에서 처리
    private boolean active; // 두 센서가 모두 등록된 상태

    public MotionSampling(Context context) {
        manager = (SensorManager) context.getApplicationContext().getSystemService(Context.SENSOR_SERVICE);
    }

    public synchronized boolean start() {
        if (active) return true;
        if (manager == null) return false;
        try {
            Sensor acceleration = manager.getDefaultSensor(Sensor.TYPE_ACCELEROMETER);
            Sensor rotation = manager.getDefaultSensor(Sensor.TYPE_GYROSCOPE_UNCALIBRATED);
            if (acceleration == null || rotation == null) return false;
            boolean accelerationEnabled = manager.registerListener(this, acceleration, SAMPLING_PERIOD_US, callbackHandler);
            boolean rotationEnabled = accelerationEnabled
                && manager.registerListener(this, rotation, SAMPLING_PERIOD_US, callbackHandler);
            active = accelerationEnabled && rotationEnabled;
            return active;
        } finally {
            // A partial registration or exception must not leave one sensor running.
            if (!active) manager.unregisterListener(this);
        }
    }

    public synchronized void stop() {
        try {
            if (manager != null) manager.unregisterListener(this);
        } finally {
            active = false;
        }
    }

    // Callbacks stay in Java. No sensor values cross JNI or modify the AR pose.
    @Override public void onSensorChanged(SensorEvent event) { }
    @Override public void onAccuracyChanged(Sensor sensor, int accuracy) { }
}
