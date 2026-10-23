# Koreyscha logo — Claude Code uchun topshiriq

Ushbu papkadagi ikonkalar bilan ilovaning eski "L" logosini almashtir.

Fayllar:
- icon.svg, favicon.svg — asosiy vektor logo (yumaloq burchakli)
- icon-192.png, icon-512.png — PWA ikonkalari (purpose: "any")
- icon-maskable-192.png, icon-maskable-512.png, icon-maskable.svg — Android uchun (purpose: "maskable")
- apple-touch-icon.png — iPhone "Add to Home Screen" uchun (180x180)
- favicon.ico, favicon-32.png — brauzer tab ikonkasi

Qilinadigan ishlar:
1. Loyihada eski logo/ikonka fayllarini top (public/, static/, assets/, manifest, index.html).
2. Shu fayllarni public (yoki static) papkasiga ko'chir, eskilarini almashtir.
3. manifest.json / manifest.webmanifest dagi "icons" ni quyidagicha qil:
   [
     {"src": "/icon-192.png", "sizes": "192x192", "type": "image/png", "purpose": "any"},
     {"src": "/icon-512.png", "sizes": "512x512", "type": "image/png", "purpose": "any"},
     {"src": "/icon-maskable-192.png", "sizes": "192x192", "type": "image/png", "purpose": "maskable"},
     {"src": "/icon-maskable-512.png", "sizes": "512x512", "type": "image/png", "purpose": "maskable"}
   ]
   va "name": "Koreyscha", "short_name": "Koreyscha", "theme_color": "#0047A0", "background_color": "#0047A0".
4. index.html <head> ga:
   <link rel="icon" href="/favicon.ico" sizes="any">
   <link rel="icon" href="/favicon.svg" type="image/svg+xml">
   <link rel="apple-touch-icon" href="/apple-touch-icon.png">
   <meta name="theme-color" content="#0047A0">
5. Ilova ichida "L" logo komponent/SVG sifatida ishlatilgan bo'lsa, uni icon.svg bilan almashtir.
6. Service worker ikonkalarni keshlasa, kesh versiyasini oshir, userlarda yangi ikonka chiqishi uchun.

Ranglar: ko'k #0047A0, qizil nuqta #E63946, oq #FFFFFF.
