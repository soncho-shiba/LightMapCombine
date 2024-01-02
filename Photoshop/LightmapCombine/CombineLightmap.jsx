
/*

// BEGIN__HARVEST_EXCEPTION_ZSTRING

<javascriptresource>
<name>$$$/JavaScripts/LightmapCombine/Menu=LightmapCombine...</name>
    <category>SonTools</category>
</javascriptresource>

// END__HARVEST_EXCEPTION_ZSTRING

*/

app.playbackDisplayDialogs = DialogModes.ALL
#target photoshop
#include "json2.js"

const COMBINED_LM_MAIN_TEX_FIT = '_CombinedLM_MainTexFit';
const COMBINED_LM_LIGHTMAP_FIT = '_CombinedLM_LightmapFit';

function main() {
    // JSONファイルを選択
    var jsonFile = File.openDialog("JSONファイルを選択してください");
    if (jsonFile == null) {
        alert("ファイルが選択されていません。");
        return;
    }

    var method = selectCompositionMethod();
    if (method === null) return; // ユーザーがキャンセルした場合
    
    
    // JSONファイルを読み込み
    // TODO: 正しいフォーマットのJSONであるかどうかを確認するvalidationを追加する
    jsonFile.open('r');
    var jsonData = jsonFile.read();
    jsonFile.close();

    // JSONをパース
    var data = JSON.parse(jsonData);
    
    if (data.objects.length > 0) {
        var obj = data.objects[0];

        if (method === "MainTexFit") {
            performMainTexFit(obj);
        } else if (method === "LightmapFit") {
            performLightmapFit(obj);
        }
        
    }
}

function selectCompositionMethod() {
    var methodDialog = new Window("dialog", "合成モードを選択");
    methodDialog.orientation = "column";
    methodDialog.alignChildren = "fill";

    var mainTexFitButton = methodDialog.add("button", undefined, "MainTexFit");
    var lightmapFitButton = methodDialog.add("button", undefined, "LightmapFit");

    var chosenMethod = null;

    mainTexFitButton.onClick = function() {
        chosenMethod = "MainTexFit";
        methodDialog.close();
    };

    lightmapFitButton.onClick = function() {
        chosenMethod = "LightmapFit";
        methodDialog.close();
    };

    methodDialog.show();
    return chosenMethod;
}


function performMainTexFit(obj) {
    // mainTextureのディレクトリパスを取得
    var mainTextureFile = new File(obj.materials[0].mainTexturePath);
    var mainTextureDir = mainTextureFile.parent.fsName;

    // mainTextureのファイル名（拡張子なし）を取得
    var mainTextureName = mainTextureFile.name.replace(/\.[^\.]+$/, '');

    if (obj.materials.length > 0 && obj.materials[0].mainTexturePath != "") {
        openAndPlace(obj.materials[0].mainTexturePath, null);
    }

    if (obj.lightmapPath !== "") {
        openAndPlace(obj.lightmapPath, app.activeDocument);
        
        //setLayerToHardLight(app.activeDocument.activeLayer);
        setLayerOpacity(app.activeDocument.activeLayer, 80);
        
        var scaleU = obj.lightmapScaleOffset.scaleU;
        var scaleV = obj.lightmapScaleOffset.scaleV;
        scaleLayer(app.activeDocument.activeLayer, 1 / scaleU, 1 / scaleV);

        var offsetU = obj.lightmapScaleOffset.offsetU;
        var offsetV = obj.lightmapScaleOffset.offsetV;
        transformLayer(app.activeDocument.activeLayer, -offsetU,-offsetV);
        
        // 保存処理を追加
        saveDocument(app.activeDocument, mainTextureDir, mainTextureName + COMBINED_LM_MAIN_TEX_FIT);
    }
}

function performLightmapFit(obj) {
    
    // mainTextureのディレクトリパスを取得
    var mainTextureFile = new File(obj.materials[0].mainTexturePath);
    var mainTextureDir = mainTextureFile.parent.fsName;

    // mainTextureのファイル名（拡張子なし）を取得
    var mainTextureName = mainTextureFile.name.replace(/\.[^\.]+$/, '');
    //TODO:mainTexが""もしくはパスに存在しない場合のエラーハンドリングを書く
    if (obj.materials.length > 0) {
        if(obj.materials[0].mainTexturePath != "") {
            openAndPlace(obj.materials[0].mainTexturePath, null);

            var scaleU = obj.lightmapScaleOffset.scaleU;
            var scaleV = obj.lightmapScaleOffset.scaleV;
            scaleLayer(app.activeDocument.activeLayer, scaleU, scaleV);

            var offsetU = obj.lightmapScaleOffset.offsetU;
            var offsetV = obj.lightmapScaleOffset.offsetV;
            transformLayer(app.activeDocument.activeLayer, offsetU, offsetV);    
        }
    }

    if (obj.lightmapPath !== "") {
        openAndPlace(obj.lightmapPath, app.activeDocument);
        
        //setLayerToHardLight(app.activeDocument.activeLayer);
        setLayerOpacity(app.activeDocument.activeLayer, 80);

        // 保存処理を追加
        saveDocument(app.activeDocument, mainTextureDir, mainTextureName + COMBINED_LM_LIGHTMAP_FIT);
    }
}

function openAndPlace(filePath, doc) {
    var file = new File(filePath);
    if (file.exists) {
        var img = app.open(file);
        img.selection.selectAll();
        img.selection.copy();

        if (doc == null) {
            // ファイル名から拡張子を除いた名前を取得し、'_CombinedLM'を追加
            var baseName = file.name.replace(/\.[^\.]+$/, '') + '_CombinedLM';
            // 新規ドキュメントを作成し、ドキュメント名を設定
            var newDoc = app.documents.add(img.width, img.height, img.resolution, baseName, NewDocumentMode.RGB, DocumentFill.TRANSPARENT);

        } else {
            // 既存のドキュメントにペースト
            app.activeDocument = doc;
        }

        var newLayer = app.activeDocument.paste();
        newLayer.name = img.name;

        img.close(SaveOptions.DONOTSAVECHANGES);
    }
}

function setLayerToHardLight(layer) {
    layer.blendMode = BlendMode.HARDLIGHT;
}

function setLayerOpacity(layer, opacity) {
    layer.opacity = opacity;
}

function scaleLayer(layer, scaleX, scaleY) {
    layer.resize(scaleX * 100, scaleY * 100, AnchorPosition.BOTTOMLEFT);
}

function transformLayer(layer, offsetX, offsetY) {
    var canvasWidth = app.activeDocument.width;
    var canvasHeight = app.activeDocument.height;
    layer.translate(canvasWidth * (offsetX/1), canvasHeight * (offsetY/1));
}
function saveDocument(doc, path, name) {
    var saveFile = new File(path + "/" + name + ".tga");
    var saveOptions = new TargaSaveOptions();
    saveOptions.resolution = TargaBitsPerPixels.THIRTYTWO;
    saveOptions.alphaChannels = true;
    saveOptions.rleCompression = false;
    doc.saveAs(saveFile, saveOptions, true, Extension.LOWERCASE);
}

main();