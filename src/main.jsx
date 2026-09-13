import React, { useEffect, useRef, useState } from 'react';
import { createRoot } from 'react-dom/client';
import JsBarcode from 'jsbarcode';
import QRCode from 'qrcode';
import './styles.css';

const copy = {
  ar: { title:'استوديو الباركود', subtitle:'أنشئ باركوداً واضحاً وجاهزاً للطباعة خلال ثوانٍ', language:'اللغة', dark:'الوضع الليلي', paper:'حجم الملصق', width:'العرض (مم)', height:'الارتفاع (مم)', font:'حجم الخط', data:'النص أو الأرقام', placeholder:'اكتب النص أو رقم المنتج هنا...', type:'نوع الباركود', numeric:'أرقام معروضة', plain:'بدون أرقام', qr:'رمز QR', generate:'إنشاء الباركود', save:'حفظ الصورة', print:'طباعة', preview:'المعاينة', ready:'جاهز للمعاينة', hint:'يمكنك استخدام العربية والإنجليزية معاً', saved:'تم حفظ الصورة بنجاح', empty:'أدخل نصاً أو أرقاماً أولاً', printFail:'تعذر بدء الطباعة' },
  en: { title:'Barcode Studio', subtitle:'Create a clear, print-ready barcode in seconds', language:'Language', dark:'Dark mode', paper:'Label size', width:'Width (mm)', height:'Height (mm)', font:'Font size', data:'Text or numbers', placeholder:'Enter text or product number...', type:'Barcode type', numeric:'Numbers shown', plain:'No numbers', qr:'QR code', generate:'Create barcode', save:'Save image', print:'Print', preview:'Preview', ready:'Ready for preview', hint:'Arabic and English text are supported', saved:'Image saved successfully', empty:'Enter text or numbers first', printFail:'Could not start printing' },
  fr: { title:'Studio Code-barres', subtitle:'Créez un code-barres prêt à imprimer', language:'Langue', dark:'Mode sombre', paper:'Taille de l’étiquette', width:'Largeur (mm)', height:'Hauteur (mm)', font:'Taille du texte', data:'Texte ou chiffres', placeholder:'Saisissez le texte ou le numéro...', type:'Type de code', numeric:'Chiffres affichés', plain:'Sans chiffres', qr:'Code QR', generate:'Créer le code', save:'Enregistrer', print:'Imprimer', preview:'Aperçu', ready:'Prêt pour l’aperçu', hint:'L’arabe et l’anglais sont pris en charge', saved:'Image enregistrée', empty:'Saisissez d’abord un texte', printFail:'Impression impossible' }
};

function App(){
 const [lang,setLang]=useState('ar'); const t=copy[lang]; const [dark,setDark]=useState(false);
 const [width,setWidth]=useState(60), [height,setHeight]=useState(30), [font,setFont]=useState(16), [value,setValue]=useState('123456789012'), [type,setType]=useState('numeric');
 const [image,setImage]=useState(''); const canvasRef=useRef(null); const previewRef=useRef(null);
 const rtl=lang==='ar';
 useEffect(()=>{ document.documentElement.lang=lang; document.documentElement.dir=rtl?'rtl':'ltr'; },[lang,rtl]);
 const generate=async()=>{ if(!value.trim()){alert(t.empty);return;} const canvas=canvasRef.current; const pxW=Math.max(240,Math.round(width*3.78)); const pxH=Math.max(120,Math.round(height*3.78)); canvas.width=pxW; canvas.height=pxH; const ctx=canvas.getContext('2d'); ctx.fillStyle='#fff';ctx.fillRect(0,0,pxW,pxH);
  if(type==='qr'){ await QRCode.toCanvas(canvas,value,{width:Math.min(pxW-30,pxH-30),margin:2,color:{dark:'#111827',light:'#ffffff'}}); }
  else { JsBarcode(canvas,value,{format:'CODE128',width:2,height:Math.max(45,pxH-48),displayValue:type==='numeric',fontSize:Number(font),margin:12,background:'#ffffff',lineColor:'#111827',textMargin:6}); }
  setImage(canvas.toDataURL('image/png'));
 };
 useEffect(()=>{generate()},[]);
 const save=async()=>{if(!image){alert(t.empty);return;} if(window.barcodeAPI) await window.barcodeAPI.saveFile({data:image,defaultPath:'barcode.png',filters:[{name:'PNG Image',extensions:['png']}]}); else {const a=document.createElement('a');a.href=image;a.download='barcode.png';a.click();} alert(t.saved)};
 const print=async()=>{if(window.barcodeAPI){const r=await window.barcodeAPI.print();if(!r.success)alert(t.printFail)}else window.print()};
 return <div className={`app ${dark?'dark':''}`}>
  <header><div className="brand"><div className="brand-mark">▥</div><div><h1>{t.title}</h1><p>{t.subtitle}</p></div></div><div className="header-actions"><label className="language">{t.language}<select value={lang} onChange={e=>setLang(e.target.value)}><option value="ar">العربية</option><option value="en">English</option><option value="fr">Français</option></select></label><button className="theme" onClick={()=>setDark(!dark)}>{dark?'☀':'◐'} {t.dark}</button></div></header>
  <main><section className="panel controls">
   <div className="section-title"><span className="step">1</span><div><h2>{t.paper}</h2><p>mm</p></div></div>
   <div className="row"><label>{t.width}<div className="input-unit"><input type="number" min="10" max="300" value={width} onChange={e=>setWidth(e.target.value)}/><span>mm</span></div></label><label>{t.height}<div className="input-unit"><input type="number" min="10" max="300" value={height} onChange={e=>setHeight(e.target.value)}/><span>mm</span></div></label><label>{t.font}<select value={font} onChange={e=>setFont(e.target.value)}><option value="12">12 px</option><option value="16">16 px</option><option value="20">20 px</option><option value="24">24 px</option></select></label></div>
   <div className="section-title second"><span className="step">2</span><div><h2>{t.data}</h2><p>{t.hint}</p></div></div><textarea value={value} onChange={e=>setValue(e.target.value)} placeholder={t.placeholder} dir="auto" />
   <div className="section-title second"><span className="step">3</span><div><h2>{t.type}</h2></div></div>
   <div className="types"><button className={type==='numeric'?'selected':''} onClick={()=>setType('numeric')}><span className="type-icon">▥</span><b>{t.numeric}</b><small>CODE 128</small></button><button className={type==='plain'?'selected':''} onClick={()=>setType('plain')}><span className="type-icon">▥</span><b>{t.plain}</b><small>CODE 128</small></button><button className={type==='qr'?'selected':''} onClick={()=>setType('qr')}><span className="type-icon qr-icon">▦</span><b>{t.qr}</b><small>QR</small></button></div>
   <button className="generate" onClick={generate}>✦ {t.generate}</button>
  </section><section className="panel preview-panel"><div className="preview-heading"><div><h2>{t.preview}</h2><p>{width} × {height} mm</p></div><span className="status">● {t.ready}</span></div><div className="preview-box" ref={previewRef}>{image?<img src={image} alt="barcode preview"/>:<div className="empty">▥</div>}</div><div className="preview-meta"><span>PNG</span><span>{type==='qr'?'QR CODE':'CODE 128'}</span></div><div className="actions"><button className="outline" onClick={save}>⇩ {t.save}</button><button className="dark-button" onClick={print}>⎙ {t.print}</button></div><canvas ref={canvasRef} className="hidden-canvas"/></section></main>
  <footer><span>Barcode Studio · Windows 10 / 11</span><span>يدعم جميع الطابعات عبر نافذة الطباعة في ويندوز</span></footer>
 </div>
}
createRoot(document.getElementById('root')).render(<App/>);
