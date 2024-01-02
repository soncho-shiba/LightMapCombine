
/*

// BEGIN__HARVEST_EXCEPTION_ZSTRING

<javascriptresource>
<name>$$$/JavaScripts/CombineLightmap/Menu=CombineLightmap...</name>
    <category>SonTools</category>
</javascriptresource>

// END__HARVEST_EXCEPTION_ZSTRING

*/

app.playbackDisplayDialogs = DialogModes.ALL
#target photoshop
#include "json2.js"

function main() {
    // JSONファイルを選択
    var jsonFile = File.openDialog("JSONファイルを選択してください");
    if (jsonFile == null) {
        alert("ファイルが選択されていません。");
        return;
    }

    // JSONファイルを読み込み
    // TODO: 正しいフォーマットのJSONであるかどうかを確認するvalidationを追加する
    jsonFile.open('r');
    var jsonData = jsonFile.read();
    jsonFile.close();

    // JSONをパース
    var data = JSON.parse(jsonData);
    if (data.objects.length > 0) {
        var obj = data.objects[0];

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

            // 保存処理を追加
            saveDocument(app.activeDocument, mainTextureDir, mainTextureName + '_CombinedLM');
        }
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
    var desc = new ActionDescriptor();

    var bounds = layer.bounds;
    var width = bounds[2].value - bounds[0].value;
    var height = bounds[3].value - bounds[1].value;
    //TODO: 百分率に変換する処理をどこに持たせるとよいか考える
    layer.resize(scaleX * 100, scaleY * 100, AnchorPosition.BOTTOMLEFT);
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