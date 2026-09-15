# Barcode Studio Android 0.4

نسخة Android مبسطة لطابعة **Xprinter XP-365B** عبر Bluetooth Classic SPP وأوامر ESC/POS.

## المميزات

- اختيار الطابعة المقترنة من الهاتف.
- الاتصال بـ XP-365B عبر Bluetooth.
- خيارات محتوى الملصق:
  - باركود فقط.
  - باركود مع سعر.
  - باركود مع سعر واسم المنتج.
- طباعة مباشرة من الهاتف.
- حفظ سجل الباركودات.
- الوضع الليلي.

تمت إزالة حجم الملصق، معايرة الطباعة، ومسح الكاميرا من شاشة إدخال المنتج. تعتمد الطباعة الآن على إعدادات الطابعة المتصلة.

## إخراج APK

هذه الميزة تحتاج Development Build لأنها تستخدم Bluetooth Classic Native Module، ولا تعمل عبر Expo Go العادي.

```bash
npm install
npx expo-doctor
npm install -g eas-cli
eas login
eas build --platform android --profile preview
```

ينتج الأمر ملف APK قابلًا للتثبيت على Android. يجب إقران XP-365B من إعدادات Bluetooth قبل الطباعة.
