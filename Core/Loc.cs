using System;
using System.Collections.Generic;
using System.Linq;

namespace XArtSkinEditor.Core;

/// <summary>UI translations. English is the default and the fallback for any missing string.</summary>
public static class Loc
{
    public sealed record Language(string Code, string NativeName);

    /// <summary>Order shown in the language selector.</summary>
    public static readonly IReadOnlyList<Language> Languages = new Language[]
    {
        new("ru", "Русский"), new("en", "English"), new("es", "Español"), new("pt", "Português"),
        new("de", "Deutsch"), new("fr", "Français"), new("it", "Italiano"), new("ja", "日本語"),
        new("zh", "简体中文"), new("uk", "Українська"), new("kk", "Қазақша"), new("pl", "Polski"),
    };

    // Column order of the translation table (after the key). The first 11 come from the rows in Build(), zh from Zh.
    private static readonly string[] Columns = { "en", "ru", "es", "pt", "de", "fr", "it", "ja", "uk", "kk", "pl", "zh" };
    private const int RowLanguages = 11;

    private static int _col;

    public static string Current { get; private set; } = "en";
    public static event Action? Changed;

    public static void SetLanguage(string code)
    {
        var idx = Array.IndexOf(Columns, code);
        if (idx < 0) idx = 0;
        if (Columns[idx] == Current) return;
        _col = idx;
        Current = Columns[idx];
        Changed?.Invoke();
    }

    /// <summary>Sets the language without raising Changed (for startup).</summary>
    public static void Init(string? code)
    {
        var idx = Array.IndexOf(Columns, code ?? "en");
        _col = idx < 0 ? 0 : idx;
        Current = Columns[_col];
    }

    public static string T(string key)
    {
        if (!Table.TryGetValue(key, out var row)) return key;
        var s = row[_col];
        return string.IsNullOrEmpty(s) ? row[0] : s;
    }

    public static string T(string key, params object[] args) => string.Format(T(key), args);

    // Lazy: Build() reads Zh, which is declared further down and would still be null in a field initializer.
    private static Dictionary<string, string[]>? _table;
    private static Dictionary<string, string[]> Table => _table ??= Build();

    private static Dictionary<string, string[]> Build()
    {
        // key, en, ru, es, pt, de, fr, it, ja, uk, kk, pl
        string[][] rows =
        {
            new[] { "menu.file", "File", "Файл", "Archivo", "Arquivo", "Datei", "Fichier", "File", "ファイル", "Файл", "Файл", "Plik" },
            new[] { "menu.edit", "Edit", "Правка", "Edición", "Editar", "Bearbeiten", "Édition", "Modifica", "編集", "Правка", "Өңдеу", "Edycja" },
            new[] { "mi.theme", "Theme", "Тема", "Tema", "Tema", "Design", "Thème", "Tema", "テーマ", "Тема", "Тақырып", "Motyw" },
            new[] { "menu.view","View", "Вид", "Ver", "Exibir", "Ansicht", "Affichage", "Visualizza", "表示", "Вигляд", "Көрініс", "Widok" },
            new[] { "mi.open", "Open PNG...", "Открыть PNG...", "Abrir PNG...", "Abrir PNG...", "PNG öffnen...", "Ouvrir un PNG...", "Apri PNG...", "PNG を開く...", "Відкрити PNG...", "PNG ашу...", "Otwórz PNG..." },
            new[] { "mi.save", "Save PNG...", "Сохранить PNG...", "Guardar PNG...", "Salvar PNG...", "PNG speichern...", "Enregistrer en PNG...", "Salva PNG...", "PNG を保存...", "Зберегти PNG...", "PNG сақтау...", "Zapisz PNG..." },
            new[] { "mi.exit", "Exit", "Выход", "Salir", "Sair", "Beenden", "Quitter", "Esci", "終了", "Вихід", "Шығу", "Zakończ" },
            new[] { "mi.undo", "Undo", "Отменить", "Deshacer", "Desfazer", "Rückgängig", "Annuler", "Annulla", "元に戻す", "Скасувати", "Болдырмау", "Cofnij" },
            new[] { "mi.redo", "Redo", "Повторить", "Rehacer", "Refazer", "Wiederholen", "Rétablir", "Ripeti", "やり直し", "Повторити", "Қайталау", "Ponów" },
            new[] { "mi.clear", "Clear canvas", "Очистить холст", "Borrar lienzo", "Limpar tela", "Leinwand leeren", "Effacer le canevas", "Pulisci tela", "キャンバスを消去", "Очистити полотно", "Кенепті тазалау", "Wyczyść płótno" },
            new[] { "mi.map", "Skin map", "Карта скина", "Mapa de skin", "Mapa da skin", "Skin-Karte", "Carte du skin", "Mappa skin", "スキンマップ", "Карта скіна", "Скин картасы", "Mapa skina" },
            new[] { "mi.grid", "Pixel grid", "Сетка пикселей", "Cuadrícula de píxeles", "Grade de pixels", "Pixelraster", "Grille de pixels", "Griglia pixel", "ピクセルグリッド", "Сітка пікселів", "Пиксель торы", "Siatka pikseli" },
            new[] { "mi.reset", "Reset 3D view", "Сбросить 3D-вид", "Restablecer vista 3D", "Redefinir visão 3D", "3D-Ansicht zurücksetzen", "Réinitialiser la vue 3D", "Reimposta vista 3D", "3D ビューをリセット", "Скинути 3D-вигляд", "3D көрінісін бастапқыға қайтару", "Resetuj widok 3D" },

            new[] { "tool.pencil", "Pencil", "Карандаш", "Lápiz", "Lápis", "Stift", "Crayon", "Matita", "鉛筆", "Олівець", "Қарындаш", "Ołówek" },
            new[] { "tool.eraser", "Eraser", "Ластик", "Borrador", "Borracha", "Radierer", "Gomme", "Gomma", "消しゴム", "Гумка", "Өшіргіш", "Gumka" },
            new[] { "tool.fill", "Fill", "Заливка", "Relleno", "Preencher", "Füllen", "Remplissage", "Riempimento", "塗りつぶし", "Заливка", "Толтыру", "Wypełnienie" },
            new[] { "tool.shade", "Lighten / Darken", "Осветлить / затемнить", "Aclarar / oscurecer", "Clarear / escurecer", "Aufhellen / Abdunkeln", "Éclaircir / assombrir", "Schiarisci / scurisci", "明るく / 暗く", "Освітлити / затемнити", "Жарықтандыру / күңгірттеу", "Rozjaśnij / przyciemnij" },
            new[] { "tip.shade", "Lighten / Darken (B)", "Осветлить / затемнить (B)", "Aclarar / oscurecer (B)", "Clarear / escurecer (B)", "Aufhellen / Abdunkeln (B)", "Éclaircir / assombrir (B)", "Schiarisci / scurisci (B)", "明るく / 暗く (B)", "Освітлити / затемнити (B)", "Жарықтандыру / күңгірттеу (B)", "Rozjaśnij / przyciemnij (B)" },
            new[] { "shade.hint", "Left button lightens, right button darkens (right-drag on empty space rotates).", "ЛКМ осветляет, ПКМ затемняет (ПКМ по пустому месту вращает).", "Clic izquierdo aclara, derecho oscurece (derecho en vacío gira).", "Botão esquerdo clareia, direito escurece (direito no vazio gira).", "Linke Taste hellt auf, rechte dunkelt ab (rechts im Leeren dreht).", "Clic gauche éclaircit, droit assombrit (droit dans le vide : rotation).", "Tasto sinistro schiarisce, destro scurisce (destro nel vuoto ruota).", "左で明るく、右で暗く（何もない所の右ドラッグで回転）。", "ЛКМ освітлює, ПКМ затемнює (ПКМ по порожньому місцю обертає).", "Сол жақ жарықтандырады, оң жақ күңгірттейді (бос жерде оң жақ — айналдыру).", "LPM rozjaśnia, PPM przyciemnia (PPM na pustym miejscu obraca)." },
            new[] { "shade.amount", "Strength", "Сила", "Intensidad", "Intensidade", "Stärke", "Intensité", "Intensità", "強さ", "Сила", "Күші", "Siła" },
            new[] { "tool.picker","Picker", "Пипетка", "Cuentagotas", "Conta-gotas", "Pipette", "Pipette", "Contagocce", "スポイト", "Піпетка", "Пипетка", "Pipeta" },

            new[] { "opt.hex.tip", "Type a color (#RRGGBB, #RGB or #RRGGBBAA) and press Enter", "Введите цвет (#RRGGBB, #RGB или #RRGGBBAA) и нажмите Enter", "Escribe un color (#RRGGBB, #RGB o #RRGGBBAA) y pulsa Enter", "Digite uma cor (#RRGGBB, #RGB ou #RRGGBBAA) e pressione Enter", "Farbe eingeben (#RRGGBB, #RGB oder #RRGGBBAA) und Enter drücken", "Saisissez une couleur (#RRGGBB, #RGB ou #RRGGBBAA) puis appuyez sur Entrée", "Digita un colore (#RRGGBB, #RGB o #RRGGBBAA) e premi Invio", "色を入力（#RRGGBB、#RGB、#RRGGBBAA）して Enter を押してください", "Введіть колір (#RRGGBB, #RGB або #RRGGBBAA) і натисніть Enter", "Түсті енгізіңіз (#RRGGBB, #RGB немесе #RRGGBBAA) да Enter басыңыз", "Wpisz kolor (#RRGGBB, #RGB lub #RRGGBBAA) i naciśnij Enter" },
            new[] { "opt.paint_on", "Paint on", "Рисовать на", "Pintar en", "Pintar em", "Malen auf", "Peindre sur", "Disegna su", "描画先", "Малювати на", "Сурет салу қабаты", "Maluj na" },
            new[] { "layer.visible", "Visible layer", "Видимый слой", "Capa visible", "Camada visível", "Sichtbare Ebene", "Calque visible", "Livello visibile", "表示中のレイヤー", "Видимий шар", "Көрінетін қабат", "Widoczna warstwa" },
            new[] { "layer.base", "Base only", "Только основа", "Solo base", "Somente base", "Nur Basis", "Base uniquement", "Solo base", "ベースのみ", "Лише основа", "Тек негіз", "Tylko baza" },
            new[] { "layer.overlay", "Overlay only", "Только верхний слой", "Solo superposición", "Somente sobreposição", "Nur Overlay", "Superposition uniquement", "Solo overlay", "オーバーレイのみ", "Лише верхній шар", "Тек үстіңгі қабат", "Tylko nakładka" },
            new[] { "opt.model", "Model", "Модель", "Modelo", "Modelo", "Modell", "Modèle", "Modello", "モデル", "Модель", "Модель", "Model" },
            new[] { "opt.grid", "Grid", "Сетка", "Cuadrícula", "Grade", "Raster", "Grille", "Griglia", "グリッド", "Сітка", "Тор", "Siatka" },

            new[] { "tip.pencil", "Pencil (P)", "Карандаш (P)", "Lápiz (P)", "Lápis (P)", "Stift (P)", "Crayon (P)", "Matita (P)", "鉛筆 (P)", "Олівець (P)", "Қарындаш (P)", "Ołówek (P)" },
            new[] { "tip.eraser", "Eraser (E)", "Ластик (E)", "Borrador (E)", "Borracha (E)", "Radierer (E)", "Gomme (E)", "Gomma (E)", "消しゴム (E)", "Гумка (E)", "Өшіргіш (E)", "Gumka (E)" },
            new[] { "tip.fill", "Fill (F)", "Заливка (F)", "Relleno (F)", "Preencher (F)", "Füllen (F)", "Remplissage (F)", "Riempimento (F)", "塗りつぶし (F)", "Заливка (F)", "Толтыру (F)", "Wypełnienie (F)" },
            new[] { "tip.picker", "Color picker (I)", "Пипетка (I)", "Cuentagotas (I)", "Conta-gotas (I)", "Pipette (I)", "Pipette (I)", "Contagocce (I)", "スポイト (I)", "Піпетка (I)", "Пипетка (I)", "Pipeta (I)" },
            new[] { "tip.undo", "Undo (Ctrl+Z)", "Отменить (Ctrl+Z)", "Deshacer (Ctrl+Z)", "Desfazer (Ctrl+Z)", "Rückgängig (Strg+Z)", "Annuler (Ctrl+Z)", "Annulla (Ctrl+Z)", "元に戻す (Ctrl+Z)", "Скасувати (Ctrl+Z)", "Болдырмау (Ctrl+Z)", "Cofnij (Ctrl+Z)" },
            new[] { "tip.redo", "Redo (Ctrl+Y)", "Повторить (Ctrl+Y)", "Rehacer (Ctrl+Y)", "Refazer (Ctrl+Y)", "Wiederholen (Strg+Y)", "Rétablir (Ctrl+Y)", "Ripeti (Ctrl+Y)", "やり直し (Ctrl+Y)", "Повторити (Ctrl+Y)", "Қайталау (Ctrl+Y)", "Ponów (Ctrl+Y)" },
            new[] { "tip.map", "Skin map (show / hide)", "Карта скина (показать / скрыть)", "Mapa de skin (mostrar / ocultar)", "Mapa da skin (mostrar / ocultar)", "Skin-Karte (ein-/ausblenden)", "Carte du skin (afficher / masquer)", "Mappa skin (mostra / nascondi)", "スキンマップ（表示 / 非表示）", "Карта скіна (показати / сховати)", "Скин картасы (көрсету / жасыру)", "Mapa skina (pokaż / ukryj)" },

            new[] { "panel.layers", "LAYERS", "СЛОИ", "CAPAS", "CAMADAS", "EBENEN", "CALQUES", "LIVELLI", "レイヤー", "ШАРИ", "ҚАБАТТАР", "WARSTWY" },
            new[] { "btn.all_on", "All on", "Все вкл.", "Todas", "Todas", "Alle an", "Tous", "Tutti", "すべて表示", "Усі ввімк.", "Қосу", "Wszystkie" },
            new[] { "btn.all_off", "All off", "Все выкл.", "Ninguna", "Nenhuma", "Alle aus", "Aucun", "Nessuno", "すべて非表示", "Усі вимк.", "Өшіру", "Żadna" },
            new[] { "layers.hint", "Eye = visible in the 3D preview", "Глаз = виден в 3D-предпросмотре", "Ojo = visible en la vista previa 3D", "Olho = visível na pré-visualização 3D", "Auge = in der 3D-Vorschau sichtbar", "Œil = visible dans l'aperçu 3D", "Occhio = visibile nell'anteprima 3D", "目 = 3D プレビューで表示", "Око = видно в 3D-перегляді", "Көз = 3D алдын ала көруде көрінеді", "Oko = widoczne w podglądzie 3D" },
            new[] { "col.base", "Base", "Основа", "Base", "Base", "Basis", "Base", "Base", "ベース", "Основа", "Негіз", "Baza" },
            new[] { "col.overlay", "Overlay", "Верхний", "Superpos.", "Sobrepos.", "Overlay", "Superpos.", "Overlay", "オーバーレイ", "Верхній", "Үстіңгі", "Nakładka" },
            new[] { "panel.color", "COLOR", "ЦВЕТ", "COLOR", "COR", "FARBE", "COULEUR", "COLORE", "カラー", "КОЛІР", "ТҮС", "KOLOR" },
            new[] { "layer.head", "Hat (head overlay)", "Шапка (верх. слой головы)", "Sombrero (cabeza)", "Chapéu (cabeça)", "Hut (Kopf-Overlay)", "Chapeau (calque de la tête)", "Cappello (overlay testa)", "帽子（頭のオーバーレイ）", "Капелюх (шар голови)", "Бас киім (бас қабаты)", "Czapka (nakładka głowy)" },
            new[] { "layer.body", "Jacket (body overlay)", "Куртка (верх. слой тела)", "Chaqueta (cuerpo)", "Jaqueta (corpo)", "Jacke (Körper-Overlay)", "Veste (calque du corps)", "Giacca (overlay corpo)", "ジャケット（体のオーバーレイ）", "Куртка (шар тіла)", "Күртеше (дене қабаты)", "Kurtka (nakładka ciała)" },
            new[] { "layer.rarm", "Right sleeve", "Правый рукав", "Manga derecha", "Manga direita", "Rechter Ärmel", "Manche droite", "Manica destra", "右袖", "Правий рукав", "Оң жең", "Prawy rękaw" },
            new[] { "layer.larm", "Left sleeve", "Левый рукав", "Manga izquierda", "Manga esquerda", "Linker Ärmel", "Manche gauche", "Manica sinistra", "左袖", "Лівий рукав", "Сол жең", "Lewy rękaw" },
            new[] { "layer.rleg", "Right pants", "Правая штанина", "Pantalón derecho", "Calça direita", "Rechtes Hosenbein", "Jambe droite", "Pantalone destro", "右ズボン", "Права штанина", "Оң балақ", "Prawa nogawka" },
            new[] { "layer.lleg", "Left pants", "Левая штанина", "Pantalón izquierdo", "Calça esquerda", "Linkes Hosenbein", "Jambe gauche", "Pantalone sinistro", "左ズボン", "Ліва штанина", "Сол балақ", "Lewa nogawka" },

            new[] { "panel.preview", "3D PREVIEW", "3D-ПРЕДПРОСМОТР", "VISTA PREVIA 3D", "PRÉ-VISUALIZAÇÃO 3D", "3D-VORSCHAU", "APERÇU 3D", "ANTEPRIMA 3D", "3D プレビュー", "3D-ПЕРЕГЛЯД", "3D АЛДЫН АЛА КӨРУ", "PODGLĄD 3D" },
            new[] { "panel.map", "SKIN MAP  ·  64×64", "КАРТА СКИНА  ·  64×64", "MAPA DE SKIN  ·  64×64", "MAPA DA SKIN  ·  64×64", "SKIN-KARTE  ·  64×64", "CARTE DU SKIN  ·  64×64", "MAPPA SKIN  ·  64×64", "スキンマップ  ·  64×64", "КАРТА СКІНА  ·  64×64", "СКИН КАРТАСЫ  ·  64×64", "MAPA SKINA  ·  64×64" },
            new[] { "status.hint", "Left mouse paints  ·  Right mouse rotates  ·  Wheel zooms", "ЛКМ — рисовать  ·  ПКМ — вращать  ·  Колесо — масштаб", "Clic izquierdo pinta  ·  Clic derecho gira  ·  Rueda: zoom", "Botão esquerdo pinta  ·  Botão direito gira  ·  Roda: zoom", "Linke Maustaste malt  ·  Rechte dreht  ·  Mausrad zoomt", "Clic gauche : dessiner  ·  Clic droit : pivoter  ·  Molette : zoom", "Tasto sinistro disegna  ·  Tasto destro ruota  ·  Rotella: zoom", "左ボタン：描画  ·  右ボタン：回転  ·  ホイール：ズーム", "ЛКМ — малювати  ·  ПКМ — обертати  ·  Колесо — масштаб", "Сол жақ батырма — сурет салу  ·  Оң жақ — айналдыру  ·  Дөңгелек — масштаб", "LPM — rysowanie  ·  PPM — obracanie  ·  Kółko — powiększenie" },

            new[] { "mcp.off", "off", "выкл.", "apagado", "desligado", "aus", "arrêté", "spento", "オフ", "вимк.", "өшірулі", "wył." },
            new[] { "mcp.port", "PORT", "ПОРТ", "PUERTO", "PORTA", "PORT", "PORT", "PORTA", "ポート", "ПОРТ", "ПОРТ", "PORT" },
            new[] { "mcp.start", "Start server", "Запустить сервер", "Iniciar servidor", "Iniciar servidor", "Server starten", "Démarrer le serveur", "Avvia server", "サーバーを起動", "Запустити сервер", "Серверді қосу", "Uruchom serwer" },
            new[] { "mcp.stop", "Stop server", "Остановить сервер", "Detener servidor", "Parar servidor", "Server stoppen", "Arrêter le serveur", "Ferma server", "サーバーを停止", "Зупинити сервер", "Серверді тоқтату", "Zatrzymaj serwer" },
            new[] { "mcp.endpoint", "ENDPOINT", "АДРЕС", "PUNTO DE ACCESO", "ENDEREÇO", "ENDPUNKT", "POINT D'ACCÈS", "ENDPOINT", "エンドポイント", "АДРЕСА", "МЕКЕНЖАЙ", "ADRES" },
            new[] { "mcp.connect", "CONNECT AN AGENT", "ПОДКЛЮЧЕНИЕ АГЕНТА", "CONECTAR UN AGENTE", "CONECTAR UM AGENTE", "AGENT VERBINDEN", "CONNECTER UN AGENT", "COLLEGA UN AGENTE", "エージェントに接続", "ПІДКЛЮЧЕННЯ АГЕНТА", "АГЕНТТІ ҚОСУ", "POŁĄCZ AGENTA" },
            new[] { "mcp.copy", "Copy", "Копировать", "Copiar", "Copiar", "Kopieren", "Copier", "Copia", "コピー", "Копіювати", "Көшіру", "Kopiuj" },
            new[] { "mcp.where", "Run it in a terminal.", "Выполните в терминале.", "Ejecútalo en una terminal.", "Execute em um terminal.", "In einem Terminal ausführen.", "À exécuter dans un terminal.", "Eseguilo in un terminale.", "ターミナルで実行します。", "Виконайте в терміналі.", "Терминалда орындаңыз.", "Uruchom w terminalu." },
            new[] { "mcp.where_file", "Add it to {0}", "Добавьте в {0}", "Añádelo a {0}", "Adicione a {0}", "In {0} eintragen", "À ajouter dans {0}", "Aggiungilo a {0}", "{0} に追加します", "Додайте в {0}", "{0} файлына қосыңыз", "Dodaj do {0}" },
            new[] { "mcp.chatgpt", "ChatGPT cannot reach 127.0.0.1: it needs a public HTTPS address. Open a tunnel, then add the printed URL + /mcp in Settings → Connectors (developer mode). The server has no password, so anyone with that URL can draw. Stop the tunnel when done.", "ChatGPT не видит 127.0.0.1: ему нужен публичный HTTPS-адрес. Откройте туннель и добавьте выданный URL + /mcp в Настройки → Коннекторы (режим разработчика). У сервера нет пароля: любой, кто знает URL, сможет рисовать. После работы закройте туннель.", "ChatGPT no alcanza 127.0.0.1: necesita una dirección HTTPS pública. Abre un túnel y añade la URL que muestre + /mcp en Ajustes → Conectores (modo desarrollador). El servidor no tiene contraseña: cualquiera con esa URL puede dibujar. Cierra el túnel al terminar.", "O ChatGPT não alcança 127.0.0.1: precisa de um endereço HTTPS público. Abra um túnel e adicione a URL exibida + /mcp em Configurações → Conectores (modo desenvolvedor). O servidor não tem senha: qualquer pessoa com essa URL pode desenhar. Feche o túnel ao terminar.", "ChatGPT erreicht 127.0.0.1 nicht: Es braucht eine öffentliche HTTPS-Adresse. Tunnel öffnen und die angezeigte URL + /mcp unter Einstellungen → Connectors (Entwicklermodus) eintragen. Der Server hat kein Passwort: Jeder mit dieser URL kann zeichnen. Tunnel danach schließen.", "ChatGPT n'atteint pas 127.0.0.1 : il faut une adresse HTTPS publique. Ouvrez un tunnel et ajoutez l'URL affichée + /mcp dans Paramètres → Connecteurs (mode développeur). Le serveur n'a pas de mot de passe : toute personne ayant l'URL peut dessiner. Fermez le tunnel ensuite.", "ChatGPT non raggiunge 127.0.0.1: serve un indirizzo HTTPS pubblico. Apri un tunnel e aggiungi l'URL mostrato + /mcp in Impostazioni → Connettori (modalità sviluppatore). Il server non ha password: chiunque abbia l'URL può disegnare. Chiudi il tunnel alla fine.", "ChatGPT は 127.0.0.1 に接続できず、公開 HTTPS アドレスが必要です。トンネルを開き、表示された URL + /mcp を 設定 → コネクタ（開発者モード）に追加してください。サーバーにパスワードはなく、URL を知っていれば誰でも描画できます。終わったらトンネルを閉じてください。", "ChatGPT не бачить 127.0.0.1: потрібна публічна HTTPS-адреса. Відкрийте тунель і додайте виданий URL + /mcp у Налаштування → Конектори (режим розробника). Сервер без пароля: будь-хто з цим URL зможе малювати. Після роботи закрийте тунель.", "ChatGPT 127.0.0.1 мекенжайына қол жеткізе алмайды: оған ашық HTTPS мекенжайы керек. Туннель ашып, шыққан URL + /mcp мекенжайын Параметрлер → Коннекторлар (әзірлеуші режимі) бөліміне қосыңыз. Серверде құпиясөз жоқ: URL білетін кез келген адам сурет сала алады. Жұмыстан кейін туннельді жабыңыз.", "ChatGPT nie widzi 127.0.0.1: potrzebuje publicznego adresu HTTPS. Otwórz tunel i dodaj wyświetlony URL + /mcp w Ustawienia → Łączniki (tryb dewelopera). Serwer nie ma hasła: każdy z tym URL może rysować. Po pracy zamknij tunel." },
            new[] { "mcp.copied", "Copied", "Скопировано", "Copiado", "Copiado", "Kopiert", "Copié", "Copiato", "コピーしました", "Скопійовано", "Көшірілді", "Skopiowano" },
            new[] { "mcp.status_off", "The server is stopped. Choose a port and start it so an AI agent can connect.", "Сервер остановлен. Выберите порт и запустите его, чтобы AI-агент мог подключиться.", "El servidor está detenido. Elige un puerto e inícialo para que un agente de IA pueda conectarse.", "O servidor está parado. Escolha uma porta e inicie-o para que um agente de IA possa se conectar.", "Der Server ist gestoppt. Port wählen und starten, damit sich ein KI-Agent verbinden kann.", "Le serveur est arrêté. Choisissez un port et démarrez-le pour qu'un agent IA puisse se connecter.", "Il server è fermo. Scegli una porta e avvialo per consentire la connessione di un agente IA.", "サーバーは停止中です。ポートを選んで起動すると、AI エージェントが接続できます。", "Сервер зупинено. Виберіть порт і запустіть його, щоб AI-агент міг підключитися.", "Сервер тоқтатылған. Порт таңдап, іске қосыңыз — сонда AI агенті қосыла алады.", "Serwer jest zatrzymany. Wybierz port i uruchom go, aby agent AI mógł się połączyć." },
            new[] { "mcp.status_on", "Running. An agent can connect to the endpoint below and draw on this canvas.", "Работает. Агент может подключиться по адресу ниже и рисовать на этом холсте.", "En ejecución. Un agente puede conectarse al punto de acceso de abajo y dibujar en este lienzo.", "Em execução. Um agente pode se conectar ao endereço abaixo e desenhar nesta tela.", "Läuft. Ein Agent kann sich mit dem Endpunkt unten verbinden und auf dieser Leinwand zeichnen.", "En cours. Un agent peut se connecter au point d'accès ci-dessous et dessiner sur ce canevas.", "In esecuzione. Un agente può collegarsi all'endpoint qui sotto e disegnare su questa tela.", "実行中です。エージェントは下のエンドポイントに接続して、このキャンバスに描画できます。", "Працює. Агент може підключитися за адресою нижче й малювати на цьому полотні.", "Жұмыс істеп тұр. Агент төмендегі мекенжайға қосылып, осы кенепте сурет сала алады.", "Działa. Agent może połączyć się z poniższym adresem i rysować na tym płótnie." },
            new[] { "mcp.err", "Could not start: {0}", "Не удалось запустить: {0}", "No se pudo iniciar: {0}", "Não foi possível iniciar: {0}", "Start fehlgeschlagen: {0}", "Impossible de démarrer : {0}", "Avvio non riuscito: {0}", "起動できませんでした: {0}", "Не вдалося запустити: {0}", "Іске қосу мүмкін болмады: {0}", "Nie udało się uruchomić: {0}" },
            new[] { "mcp.busy", "port {0} is already in use. Choose another port.", "порт {0} уже занят. Выберите другой порт.", "el puerto {0} ya está en uso. Elige otro puerto.", "a porta {0} já está em uso. Escolha outra porta.", "Port {0} wird bereits verwendet. Wähle einen anderen Port.", "le port {0} est déjà utilisé. Choisissez un autre port.", "la porta {0} è già in uso. Scegli un'altra porta.", "ポート {0} は既に使用されています。別のポートを選んでください。", "порт {0} уже зайнятий. Виберіть інший порт.", "{0} порты бос емес. Басқа порт таңдаңыз.", "port {0} jest już zajęty. Wybierz inny port." },

            new[] { "dlg.clear.title", "Clear canvas?", "Очистить холст?", "¿Borrar el lienzo?", "Limpar a tela?", "Leinwand leeren?", "Effacer le canevas ?", "Pulire la tela?", "キャンバスを消去しますか？", "Очистити полотно?", "Кенепті тазалау керек пе?", "Wyczyścić płótno?" },
            new[] { "dlg.clear.msg", "This erases the whole skin. You can undo it with Ctrl+Z.", "Весь скин будет стёрт. Это можно отменить через Ctrl+Z.", "Se borrará todo el skin. Puedes deshacerlo con Ctrl+Z.", "Isso apaga toda a skin. Você pode desfazer com Ctrl+Z.", "Der gesamte Skin wird gelöscht. Mit Strg+Z kannst du das rückgängig machen.", "Tout le skin sera effacé. Vous pouvez annuler avec Ctrl+Z.", "Verrà cancellata l'intera skin. Puoi annullare con Ctrl+Z.", "スキン全体が消去されます。Ctrl+Z で元に戻せます。", "Весь скін буде стерто. Це можна скасувати через Ctrl+Z.", "Бүкіл скин өшіріледі. Мұны Ctrl+Z арқылы болдырмауға болады.", "Cały skin zostanie usunięty. Możesz to cofnąć skrótem Ctrl+Z." },
            new[] { "dlg.clear.ok", "Clear", "Очистить", "Borrar", "Limpar", "Leeren", "Effacer", "Pulisci", "消去", "Очистити", "Тазалау", "Wyczyść" },
            new[] { "dlg.cancel", "Cancel", "Отмена", "Cancelar", "Cancelar", "Abbrechen", "Annuler", "Annulla", "キャンセル", "Скасувати", "Бас тарту", "Anuluj" },

            new[] { "msg.opened", "Opened {0}: detected {1} model", "Открыт {0}: определена модель {1}", "Abierto {0}: modelo {1} detectado", "Aberto {0}: modelo {1} detectado", "{0} geöffnet: Modell {1} erkannt", "{0} ouvert : modèle {1} détecté", "Aperto {0}: rilevato modello {1}", "{0} を開きました：{1} モデルを検出", "Відкрито {0}: визначено модель {1}", "{0} ашылды: {1} моделі анықталды", "Otwarto {0}: wykryto model {1}" },
            new[] { "msg.saved", "Saved {0}", "Сохранено: {0}", "Guardado {0}", "Salvo {0}", "{0} gespeichert", "{0} enregistré", "{0} salvato", "{0} を保存しました", "Збережено: {0}", "{0} сақталды", "Zapisano {0}" },
            new[] { "msg.open_failed", "Open failed: {0}", "Не удалось открыть: {0}", "No se pudo abrir: {0}", "Falha ao abrir: {0}", "Öffnen fehlgeschlagen: {0}", "Échec de l'ouverture : {0}", "Apertura non riuscita: {0}", "開けませんでした: {0}", "Не вдалося відкрити: {0}", "Ашу мүмкін болмады: {0}", "Nie udało się otworzyć: {0}" },
            new[] { "msg.bad_color", "Invalid color. Use #RRGGBB, #RGB or #RRGGBBAA.", "Неверный цвет. Используйте #RRGGBB, #RGB или #RRGGBBAA.", "Color no válido. Usa #RRGGBB, #RGB o #RRGGBBAA.", "Cor inválida. Use #RRGGBB, #RGB ou #RRGGBBAA.", "Ungültige Farbe. Verwende #RRGGBB, #RGB oder #RRGGBBAA.", "Couleur invalide. Utilisez #RRGGBB, #RGB ou #RRGGBBAA.", "Colore non valido. Usa #RRGGBB, #RGB o #RRGGBBAA.", "無効な色です。#RRGGBB、#RGB、#RRGGBBAA を使ってください。", "Неправильний колір. Використовуйте #RRGGBB, #RGB або #RRGGBBAA.", "Қате түс. #RRGGBB, #RGB немесе #RRGGBBAA пайдаланыңыз.", "Nieprawidłowy kolor. Użyj #RRGGBB, #RGB lub #RRGGBBAA." },
            new[] { "file.open_title", "Open skin", "Открыть скин", "Abrir skin", "Abrir skin", "Skin öffnen", "Ouvrir un skin", "Apri skin", "スキンを開く", "Відкрити скін", "Скинді ашу", "Otwórz skin" },
            new[] { "file.save_title", "Save skin", "Сохранить скин", "Guardar skin", "Salvar skin", "Skin speichern", "Enregistrer le skin", "Salva skin", "スキンを保存", "Зберегти скін", "Скинді сақтау", "Zapisz skin" },

            new[] { "tab.default", "Skin {0}", "Скин {0}", "Skin {0}", "Skin {0}", "Skin {0}", "Skin {0}", "Skin {0}", "スキン {0}", "Скін {0}", "Скин {0}", "Skin {0}" },
            new[] { "tab.new", "New tab (Ctrl+T)", "Новая вкладка (Ctrl+T)", "Nueva pestaña (Ctrl+T)", "Nova guia (Ctrl+T)", "Neuer Tab (Strg+T)", "Nouvel onglet (Ctrl+T)", "Nuova scheda (Ctrl+T)", "新しいタブ (Ctrl+T)", "Нова вкладка (Ctrl+T)", "Жаңа қойынды (Ctrl+T)", "Nowa karta (Ctrl+T)" },
            new[] { "tab.close", "Close tab (Ctrl+W)", "Закрыть вкладку (Ctrl+W)", "Cerrar pestaña (Ctrl+W)", "Fechar guia (Ctrl+W)", "Tab schließen (Strg+W)", "Fermer l'onglet (Ctrl+W)", "Chiudi scheda (Ctrl+W)", "タブを閉じる (Ctrl+W)", "Закрити вкладку (Ctrl+W)", "Қойындыны жабу (Ctrl+W)", "Zamknij kartę (Ctrl+W)" },
            new[] { "mi.new", "New tab", "Новая вкладка", "Nueva pestaña", "Nova guia", "Neuer Tab", "Nouvel onglet", "Nuova scheda", "新しいタブ", "Нова вкладка", "Жаңа қойынды", "Nowa karta" },
            new[] { "mi.close", "Close tab", "Закрыть вкладку", "Cerrar pestaña", "Fechar guia", "Tab schließen", "Fermer l'onglet", "Chiudi scheda", "タブを閉じる", "Закрити вкладку", "Қойындыны жабу", "Zamknij kartę" },
            new[] { "dlg.close.title", "Close “{0}”?", "Закрыть «{0}»?", "¿Cerrar «{0}»?", "Fechar “{0}”?", "„{0}“ schließen?", "Fermer « {0} » ?", "Chiudere «{0}»?", "「{0}」を閉じますか？", "Закрити «{0}»?", "«{0}» жабу керек пе?", "Zamknąć „{0}”?" },
            new[] { "dlg.close.msg", "This skin has unsaved changes.", "В этом скине есть несохранённые изменения.", "Este skin tiene cambios sin guardar.", "Esta skin tem alterações não salvas.", "Dieser Skin hat ungespeicherte Änderungen.", "Ce skin contient des modifications non enregistrées.", "Questa skin ha modifiche non salvate.", "このスキンには未保存の変更があります。", "У цьому скіні є незбережені зміни.", "Бұл скинде сақталмаған өзгерістер бар.", "Ten skin ma niezapisane zmiany." },
            new[] { "dlg.close.ok", "Close", "Закрыть", "Cerrar", "Fechar", "Schließen", "Fermer", "Chiudi", "閉じる", "Закрити", "Жабу", "Zamknij" },
            new[] { "msg.restored", "Restored {0} tab(s) from your last session", "Восстановлено вкладок из прошлой сессии: {0}", "Se restauraron {0} pestaña(s) de la sesión anterior", "{0} guia(s) restaurada(s) da sessão anterior", "{0} Tab(s) aus der letzten Sitzung wiederhergestellt", "{0} onglet(s) restauré(s) depuis la dernière session", "{0} scheda/e ripristinata/e dall'ultima sessione", "前回のセッションから {0} 個のタブを復元しました", "Відновлено вкладок з минулої сесії: {0}", "Алдыңғы сеанстан {0} қойынды қалпына келтірілді", "Przywrócono karty z poprzedniej sesji: {0}" },

            new[] { "part.head", "head", "голова", "cabeza", "cabeça", "Kopf", "tête", "testa", "頭", "голова", "бас", "głowa" },
            new[] { "part.body", "body", "тело", "cuerpo", "corpo", "Körper", "corps", "corpo", "体", "тіло", "дене", "ciało" },
            new[] { "part.rarm", "right arm", "правая рука", "brazo derecho", "braço direito", "rechter Arm", "bras droit", "braccio destro", "右腕", "права рука", "оң қол", "prawa ręka" },
            new[] { "part.larm", "left arm", "левая рука", "brazo izquierdo", "braço esquerdo", "linker Arm", "bras gauche", "braccio sinistro", "左腕", "ліва рука", "сол қол", "lewa ręka" },
            new[] { "part.rleg", "right leg", "правая нога", "pierna derecha", "perna direita", "rechtes Bein", "jambe droite", "gamba destra", "右脚", "права нога", "оң аяқ", "prawa noga" },
            new[] { "part.lleg", "left leg", "левая нога", "pierna izquierda", "perna esquerda", "linkes Bein", "jambe gauche", "gamba sinistra", "左脚", "ліва нога", "сол аяқ", "lewa noga" },
            new[] { "overlay.word", "overlay", "верхний слой", "superposición", "sobreposição", "Overlay", "superposition", "overlay", "オーバーレイ", "верхній шар", "үстіңгі қабат", "nakładka" },
            new[] { "face.left", "left", "слева", "izquierda", "esquerda", "links", "gauche", "sinistra", "左", "зліва", "сол жақ", "lewa" },
            new[] { "face.right", "right", "справа", "derecha", "direita", "rechts", "droite", "destra", "右", "справа", "оң жақ", "prawa" },
            new[] { "face.top", "top", "верх", "arriba", "topo", "oben", "haut", "alto", "上", "верх", "үсті", "góra" },
            new[] { "face.bottom", "bottom", "низ", "abajo", "base", "unten", "bas", "basso", "下", "низ", "асты", "dół" },
            new[] { "face.front", "front", "перед", "frente", "frente", "vorne", "face", "fronte", "前", "перед", "алды", "przód" },
            new[] { "face.back", "back", "спина", "espalda", "costas", "hinten", "dos", "retro", "後", "спина", "арты", "tył" },
        };

        var dict = new Dictionary<string, string[]>(rows.Length);
        foreach (var r in rows)
        {
            if (r.Length != RowLanguages + 1)
                throw new InvalidOperationException($"Translation row '{r[0]}' has {r.Length - 1} values, expected {RowLanguages}.");
            // A missing zh entry stays empty and falls back to English in T().
            Zh.TryGetValue(r[0], out var zh);
            dict[r[0]] = r.Skip(1).Append(zh ?? "").ToArray();
        }
        return dict;
    }

    // Simplified Chinese, keyed like the rows above.
    private static readonly Dictionary<string, string> Zh = new()
    {
        ["menu.file"] = "文件", ["menu.edit"] = "编辑", ["menu.view"] = "视图", ["mi.theme"] = "主题",
        ["mi.open"] = "打开 PNG...", ["mi.save"] = "保存 PNG...", ["mi.exit"] = "退出",
        ["mi.undo"] = "撤销", ["mi.redo"] = "重做", ["mi.clear"] = "清空画布",
        ["mi.map"] = "皮肤贴图", ["mi.grid"] = "像素网格", ["mi.reset"] = "重置 3D 视图",
        ["tool.pencil"] = "铅笔", ["tool.eraser"] = "橡皮擦", ["tool.fill"] = "填充", ["tool.picker"] = "取色器",
        ["tool.shade"] = "变亮 / 变暗", ["tip.shade"] = "变亮 / 变暗 (B)", ["shade.hint"] = "左键变亮，右键变暗（在空白处按右键拖动可旋转）。", ["shade.amount"] = "强度",
        ["opt.hex.tip"] = "输入颜色（#RRGGBB、#RGB 或 #RRGGBBAA）后按回车",
        ["opt.paint_on"] = "绘制到", ["layer.visible"] = "可见图层", ["layer.base"] = "仅基础层", ["layer.overlay"] = "仅外层",
        ["opt.model"] = "模型", ["opt.grid"] = "网格",
        ["tip.pencil"] = "铅笔 (P)", ["tip.eraser"] = "橡皮擦 (E)", ["tip.fill"] = "填充 (F)", ["tip.picker"] = "取色器 (I)",
        ["tip.undo"] = "撤销 (Ctrl+Z)", ["tip.redo"] = "重做 (Ctrl+Y)", ["tip.map"] = "皮肤贴图（显示 / 隐藏）",
        ["panel.layers"] = "图层", ["btn.all_on"] = "全部显示", ["btn.all_off"] = "全部隐藏",
        ["layers.hint"] = "眼睛 = 在 3D 预览中可见", ["col.base"] = "基础层", ["col.overlay"] = "外层", ["panel.color"] = "颜色",
        ["layer.head"] = "帽子（头部外层）", ["layer.body"] = "外套（身体外层）",
        ["layer.rarm"] = "右袖", ["layer.larm"] = "左袖", ["layer.rleg"] = "右裤腿", ["layer.lleg"] = "左裤腿",
        ["panel.preview"] = "3D 预览", ["panel.map"] = "皮肤贴图  ·  64×64",
        ["status.hint"] = "左键绘制  ·  右键旋转  ·  滚轮缩放",
        ["mcp.off"] = "已关闭", ["mcp.port"] = "端口", ["mcp.start"] = "启动服务器", ["mcp.stop"] = "停止服务器",
        ["mcp.endpoint"] = "地址", ["mcp.connect"] = "连接代理", ["mcp.copy"] = "复制", ["mcp.copied"] = "已复制",
        ["mcp.where"] = "在终端中运行。", ["mcp.where_file"] = "添加到 {0}",
        ["mcp.chatgpt"] = "ChatGPT 无法访问 127.0.0.1，需要公网 HTTPS 地址。请先开启隧道，再把显示的 URL + /mcp 添加到 设置 → 连接器（开发者模式）。服务器没有密码，任何知道该 URL 的人都能绘制。用完请关闭隧道。",
        ["mcp.status_off"] = "服务器已停止。选择端口并启动，AI 代理即可连接。",
        ["mcp.status_on"] = "运行中。代理可连接下方地址并在此画布上绘制。",
        ["mcp.err"] = "无法启动：{0}", ["mcp.busy"] = "端口 {0} 已被占用，请选择其他端口。",
        ["dlg.clear.title"] = "清空画布？", ["dlg.clear.msg"] = "这将擦除整个皮肤，可按 Ctrl+Z 撤销。",
        ["dlg.clear.ok"] = "清空", ["dlg.cancel"] = "取消",
        ["msg.opened"] = "已打开 {0}：检测到 {1} 模型", ["msg.saved"] = "已保存 {0}", ["msg.open_failed"] = "打开失败：{0}",
        ["msg.bad_color"] = "颜色无效。请使用 #RRGGBB、#RGB 或 #RRGGBBAA。",
        ["file.open_title"] = "打开皮肤", ["file.save_title"] = "保存皮肤",
        ["tab.default"] = "皮肤 {0}", ["tab.new"] = "新标签页 (Ctrl+T)", ["tab.close"] = "关闭标签页 (Ctrl+W)",
        ["mi.new"] = "新标签页", ["mi.close"] = "关闭标签页",
        ["dlg.close.title"] = "关闭“{0}”？", ["dlg.close.msg"] = "此皮肤有未保存的更改。", ["dlg.close.ok"] = "关闭",
        ["msg.restored"] = "已从上次会话恢复 {0} 个标签页",
        ["part.head"] = "头部", ["part.body"] = "身体", ["part.rarm"] = "右臂", ["part.larm"] = "左臂",
        ["part.rleg"] = "右腿", ["part.lleg"] = "左腿", ["overlay.word"] = "外层",
        ["face.left"] = "左", ["face.right"] = "右", ["face.top"] = "上", ["face.bottom"] = "下", ["face.front"] = "前", ["face.back"] = "后",
    };
}
