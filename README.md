# UnityBugReporter

**WIP**

A plugin to pull and fill issues on bug reporter directly from inside Unity

It rely on a backend that will handles pulling & logging the issues to your choosen platforms.

Currently builtin are :
 - a Github backend
 - a Gitlab backend (rely on entering the path to the api, to support private instance)

## Usage

Once the folder copied to your project, open the bug lister at least once
(top menu Bug Reproter/Open) to choose the backend and enter the repository path (for Github and Gitlab, it's in the form owner/reponame, 
e.g. for this one Sirithang/UnityBugReporter)

Each backend will ask for different mean of authentification if needed. e.g.
- The github backend will ask for a OAuth personal token, can be generated through your developer settings in your Github setting page. **Be sure to give it repo access!**
- The gitlab backend will ask for a personal token that can be generated in your Gitlab setting. **Give it api access**

Once all that done, you can press F12 in the scene view or when in play mode to
make the bug reporter appear. Fill it, press Send and an issue is created on the
issue tracker.

Opening the bug tracker you can pull issues matching the setup filters. Issue that were logged through the Bug reporter will have information regarding their position & scene,
so if an issue is part of the current scene, an icone at the position where it was logged will appear in the scene view.

You can click this icone to make it the active issue and open it's description in the bug tracker, and click the GoTo button in the bug tracker to sync the scene view camera to
the camera position/rotation when the bug was logged.

### Note on settings

The system save your choosen backend and the access token **in clear** in your project. It is however stored in the **Library** folder, and since that folder shouldn't be
commited, your information will stay on your machine. It does mean that you will need to re-setup the bug tracker on new machine.

If you require to change the backend, just delete that file for now (Todo : make a settings windows to change all that).

If a request fail because the access token became invalid or other reason, the backend should take care of clearing the setting to force the user to re-enter it.

## Note on Unity BT Url

The bug reporter insert at the end of the description a pseudo-url of the form
"unitybt://float-float-float-float-float-float-float-float-guid".

This encode the position of the pivot (3 floats), the distance of the camera (1 float), the rotation of the camera (4 floats) and guid
of the scene in which the editor was when F12 was pressed.

Those setting may seem weird (instead of just position & rotation) but that's how the scene view camera encode its position/rotation, so easier
to scave those and just use a distance of 0.0001 (as 0 fail in scene view) with the position of the camera as pivot when reporting from the gameview.

That way we can sync more easily the scene view camera to the game view one.

