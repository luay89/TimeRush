import React, { useEffect, useState } from 'react';
import { Alert, FlatList, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { CameraView, useCameraPermissions } from 'expo-camera';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { StatusBar } from 'expo-status-bar';

const KEY='barcode-history-v03';
type Item={id:string;value:string;name:string;created:string};
export default function App(){
 const [dark,setDark]=useState(false), [scanning,setScanning]=useState(false), [permission,requestPermission]=useCameraPermissions();
 const [value,setValue]=useState(''),[name,setName]=useState(''),[history,setHistory]=useState<Item[]>([]);
 const colors=dark?{bg:'#111827',card:'#1f2937',text:'#f3f4f6',muted:'#9ca3af',border:'#374151',primary:'#7772f5'}:{bg:'#f5f7fb',card:'#fff',text:'#172033',muted:'#7b8494',border:'#e5e7eb',primary:'#625ff2'};
 useEffect(()=>{AsyncStorage.getItem(KEY).then(x=>x&&setHistory(JSON.parse(x)))} ,[]);
 const save=(next:Item[])=>{setHistory(next);AsyncStorage.setItem(KEY,JSON.stringify(next))};
 const add=()=>{if(!value.trim()){Alert.alert('تنبيه','أدخل رقم الباركود أولاً');return}const item={id:Date.now().toString(),value:value.trim(),name:name.trim(),created:new Date().toLocaleString()};save([item,...history].slice(0,30));Alert.alert('تم الحفظ','تمت إضافة الباركود إلى السجل')};
 const scan=async()=>{if(!permission?.granted){const r=await requestPermission();if(!r.granted){Alert.alert('صلاحية الكاميرا','يرجى السماح باستخدام الكاميرا من إعدادات الهاتف');return}}setScanning(true)};
 if(scanning)return <View style={styles.cameraPage}><StatusBar style="light"/><CameraView style={styles.camera} facing="back" barcodeScannerSettings={{barcodeTypes:['qr','ean13','ean8','code128','code39','upc_a','upc_e','itf14','datamatrix']}} onBarcodeScanned={({data})=>{setValue(data);setScanning(false)}}><View style={styles.scanFrame}/><Text style={styles.cameraHint}>وجّه الكاميرا نحو الباركود</Text><Pressable style={styles.close} onPress={()=>setScanning(false)}><Text style={styles.closeText}>إغلاق</Text></Pressable></CameraView></View>;
 return <View style={[styles.page,{backgroundColor:colors.bg}]}><StatusBar style={dark?'light':'dark'}/><View style={[styles.header,{borderBottomColor:colors.border}]}><View><Text style={[styles.title,{color:colors.text}]}>استوديو الباركود</Text><Text style={[styles.subtitle,{color:colors.muted}]}>نسخة Android · 0.3</Text></View><Pressable onPress={()=>setDark(!dark)} style={styles.theme}><Text style={{color:colors.text,fontSize:18}}>{dark?'☀':'◐'}</Text></Pressable></View><FlatList data={history} keyExtractor={x=>x.id} ListHeaderComponent={<View><View style={[styles.card,{backgroundColor:colors.card,borderColor:colors.border}]}><Text style={[styles.label,{color:colors.muted}]}>رقم الباركود</Text><View style={styles.valueRow}><TextInput value={value} onChangeText={setValue} placeholder="اكتب الرقم أو امسحه بالكاميرا" placeholderTextColor={colors.muted} style={[styles.input,{color:colors.text,borderColor:colors.border}]}/><Pressable style={[styles.scanButton,{backgroundColor:colors.primary}]} onPress={scan}><Text style={styles.buttonText}>▣ مسح</Text></Pressable></View><Text style={[styles.label,{color:colors.muted}]}>اسم المنتج</Text><TextInput value={name} onChangeText={setName} placeholder="مثال: قهوة عربية" placeholderTextColor={colors.muted} style={[styles.input,{color:colors.text,borderColor:colors.border}]}/><Pressable style={[styles.saveButton,{backgroundColor:colors.primary}]} onPress={add}><Text style={styles.buttonText}>حفظ في السجل</Text></Pressable></View><Text style={[styles.historyTitle,{color:colors.text}]}>آخر الباركودات</Text></View>} renderItem={({item})=><View style={[styles.item,{backgroundColor:colors.card,borderColor:colors.border}]}><View><Text style={[styles.itemName,{color:colors.text}]}>{item.name||'بدون اسم'}</Text><Text style={[styles.itemValue,{color:colors.muted}]}>{item.value} · {item.created}</Text></View><Pressable onPress={()=>setValue(item.value)}><Text style={{color:colors.primary,fontWeight:'700'}}>استخدام</Text></Pressable></View>} ListEmptyComponent={<Text style={[styles.empty,{color:colors.muted}]}>لا توجد عناصر محفوظة بعد</Text>} contentContainerStyle={styles.content}/></View>
}
const styles=StyleSheet.create({
 page:{flex:1}, header:{paddingHorizontal:20,paddingTop:58,paddingBottom:18,flexDirection:'row',justifyContent:'space-between',alignItems:'center',borderBottomWidth:1},
 title:{fontSize:24,fontWeight:'800'}, subtitle:{fontSize:12,marginTop:3}, theme:{padding:8}, content:{padding:16,paddingBottom:40},
 card:{borderWidth:1,borderRadius:18,padding:18,marginBottom:24}, label:{fontSize:12,marginBottom:7,marginTop:5}, valueRow:{flexDirection:'row',gap:8,alignItems:'center'},
 input:{borderWidth:1,borderRadius:10,height:46,paddingHorizontal:12,fontSize:14,flex:1,marginBottom:12}, scanButton:{height:46,borderRadius:10,paddingHorizontal:15,justifyContent:'center'},
 saveButton:{height:46,borderRadius:10,justifyContent:'center',alignItems:'center',marginTop:4}, buttonText:{color:'#fff',fontWeight:'800',fontSize:13},
 historyTitle:{fontSize:18,fontWeight:'800',marginBottom:12}, item:{borderWidth:1,borderRadius:13,padding:14,marginBottom:9,flexDirection:'row',alignItems:'center',justifyContent:'space-between'},
 itemName:{fontWeight:'700',fontSize:14}, itemValue:{fontSize:11,marginTop:4}, empty:{textAlign:'center',marginTop:30}, cameraPage:{flex:1,backgroundColor:'#000'},
 camera:{flex:1,justifyContent:'center',alignItems:'center'}, scanFrame:{width:270,height:170,borderWidth:3,borderColor:'#7772f5',borderRadius:18},
 cameraHint:{color:'#fff',fontWeight:'700',marginTop:18}, close:{position:'absolute',bottom:46,backgroundColor:'#fff',paddingHorizontal:28,paddingVertical:12,borderRadius:12}, closeText:{color:'#172033',fontWeight:'800'}
});
