# Barcode Studio

تطبيق Barcode Studio لإنشاء وطباعة الباركودات على Windows وAndroid.

## Windows 0.4

- إزالة قسم حجم الملصق وتفاصيله.
- إزالة مسح الكاميرا من بيانات المنتج.
- إزالة معايرة الطباعة؛ تتم الطباعة حسب الطابعة المتصلة ونافذة Windows.
- خيارات محتوى الملصق: باركود فقط، باركود مع سعر، باركود مع سعر واسم المنتج.
- دعم Code 128 وQR، الحفظ، الطباعة، عدد النسخ، السجل، والوضع الليلي.
- المعاينة تتحدث تلقائيًا عند تغيير النص أو النوع أو خيار المحتوى.

## Android 0.4

- دعم Xprinter XP-365B عبر Bluetooth Classic SPP وESC/POS.
- اختيار الطابعة المقترنة والطباعة المباشرة.
- نفس خيارات محتوى الملصق الثلاثة.
- إزالة الكاميرا وأذوناتها من النسخة المبسطة.
- يتطلب Bluetooth Development Build، ولا تعمل الطباعة الأصلية عبر Expo Go.

```bash
# Windows
npm install
npm run dist

# Android APK
cd android
npm install
npx eas-cli login
npx eas build --platform android --profile preview
```
