# Barcode Studio Android 0.3

نسخة الهاتف تحتوي على:

- مسح QR والباركود باستخدام كاميرا الجهاز.
- دعم Code 128 وEAN وUPC وITF وData Matrix.
- إدخال الرقم يدويًا أو بالكاميرا.
- إضافة اسم المنتج.
- حفظ آخر النتائج محليًا على الجهاز.
- إعادة استخدام نتيجة سابقة.
- الوضع الليلي.

## التشغيل

```bash
npm install
npx expo start
```

## إخراج APK

يتطلب الأمر حساب Expo/EAS:

```bash
npm install -g eas-cli
eas login
eas build --platform android --profile preview
```

ينتج ملف APK قابلًا للتثبيت على Android.
