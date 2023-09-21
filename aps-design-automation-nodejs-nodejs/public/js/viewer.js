/// import * as Autodesk from "@types/forge-viewer";

async function getAccessToken(callback) {
    try {
        const resp = await fetch('/api/auth/token');
        if (!resp.ok) {
            throw new Error(await resp.text());
        }
        const { access_token, expires_in } = await resp.json();
        callback(access_token, expires_in);
    } catch (err) {
        alert('Could not obtain access token. See the console for more details.');
        console.error(err);
    }
}

function launchViewer(urn) {
    var options = {
      env: 'AutodeskProduction2',
      api: "streamingV2",
      getAccessToken: getAccessToken
    };
  
    Autodesk.Viewing.Initializer(options, () => {
      document.getElementById('apsViewer').innerHTML = "";
      viewer = new Autodesk.Viewing.GuiViewer3D(document.getElementById('apsViewer'));
      viewer.start();
      var documentId = 'urn:' + urn;
      Autodesk.Viewing.Document.load(documentId, onDocumentLoadSuccess, onDocumentLoadFailure);
      //$('.adsk-viewing-viewer').css({ "height": '95%' })
  
    });
  }

  
function onDocumentLoadSuccess(doc) {
    // if a viewableId was specified, load that view, otherwise the default view
    var viewables = doc.getRoot().getDefaultGeometry();
    
    
      viewer.loadDocumentNode(doc, viewables, {}).then(async i => {
  
      }); 
  }
  
  function onDocumentLoadFailure(viewerErrorCode, viewerErrorMsg) {
    console.error('onDocumentLoadFailure() - errorCode:' + viewerErrorCode + '\n- errorMessage:' + viewerErrorMsg);
  }
  