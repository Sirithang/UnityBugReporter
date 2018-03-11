# UnityBugReporter

**WIP**

A plugin to pull and fill issues on bug reporter directly from inside Unity

It only contain a Github backend for now, but the issue pull & filling is abstracted
so it should be simpler to implement different backend (Gitlab, Bitbucket etc..)

## Usage

Once the folder copied to your project, open the bug lister at least once
(top menu Bug Reproter/Open) to choose the backend (only github for now) and
enter the repository path (in the form owner/reponame, e.g. for this one
  Sirithang/UnityBugReporter)

The system will ask for a OAuth Personal access tokens, that you can create in
your settings -> Developer Settings in Github. **Give it repo and users access**
(I choose a bit randomly, need to check what access I really need just for issues
  writing/pulling.)


Once all that done, you can press F12 in the scene view or when in play mode to
make the bug reporter appear. Fill it, press Send and an issue is created on the
issue tracker.

## Unity BT Url

The bug reporter insert at the end of the description a pseudo-url of the form
"unitybt://float-float-float-float-float-float-float-guid".

This encode the position (3 floats), rotation (4 floats) of the camera and guid
of the scene in which the editor was when F12 was pressed.

Used by the system to display the position of the bug in editor & auto move to
that position through the bug list.

### Note on saved settings

Settings are saved in the Library folder, as the Personal Access token is written in plain there, so it won't be commited to the repo
That mean pulling the repo somewhere else or deleting the Library folder will need to re-enter the personal access token & project name.

## TODO

- [X] parse unitybt url & use that in editor to move to that place
- [X] display issue with a unitybt link in the sceneview where they were taken.
- [X] offer way to filter before pulling issues
- [ ] write more backend (gitlab to start)
- [ ] automate creating the OAuth using github login/password
