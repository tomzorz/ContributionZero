// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using Android.App;
using Android.OS;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using Google.AR.Core;
using Google.AR.Sceneform;
using Google.AR.Sceneform.Rendering;
using Google.AR.Sceneform.UX;
using Java.Util.Concurrent;
using Microsoft.Azure.SpatialAnchors;
using SampleXamarin.AnchorSharing;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Android.Content;
using Plugin.Clipboard;

namespace SampleXamarin
{
    [Activity(Label = "AzureSpatialAnchorsSharedActivity")]
    public class AzureSpatialAnchorsSharedActivity : AppCompatActivity, Node.IOnTouchListener
    {
        private static Material failedColor;

        private static Material foundColor;

        private static Material readyColor;

        private static Material savedColor;

        private static Material selectedColor;

        private readonly ConcurrentDictionary<string, AnchorVisual> anchorVisuals = new ConcurrentDictionary<string, AnchorVisual>();

        private readonly object renderLock = new object();

        private AnchorSharingServiceClient anchorSharingServiceClient;

        private string anchorId = string.Empty;

        private EditText anchorNumInput;

        private ArFragment arFragment;

        private AzureSpatialAnchorsManager cloudAnchorManager;

        private Button createButton;

        private DemoStep currentStep = DemoStep.Start;

        private TextView editTextInfo;

        private Button exitButton;

        private string feedbackText;

        private Button locateButton;

        private ArSceneView sceneView;

        private TextView textView;

        private LinearLayout backingPlateTwo;

        private readonly MindrService.MindrService _mindrService;

        public AzureSpatialAnchorsSharedActivity()
        {
            _mindrService = new MindrService.MindrService();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            DestroySession();
        }

        protected override void OnResume()
        {
            base.OnResume();

            // ArFragment of Sceneform automatically requests the camera permission before creating the AR session,
            // so we don't need to request the camera permission explicitly.
            // This will cause onResume to be called again after the user responds to the permission request.
            if (!SceneformHelper.HasCameraPermission(this))
            {
                return;
            }

            if (sceneView?.Session is null && !SceneformHelper.TrySetupSessionForSceneView(this, sceneView))
            {
                // Exception will be logged and SceneForm will handle any ARCore specific issues.
                Finish();
                return;
            }

            if (string.IsNullOrWhiteSpace(AccountDetails.SpatialAnchorsAccountId) || AccountDetails.SpatialAnchorsAccountId == "Set me"
                    || string.IsNullOrWhiteSpace(AccountDetails.SpatialAnchorsAccountKey) || AccountDetails.SpatialAnchorsAccountKey == "Set me")
            {
                Toast.MakeText(this, $"\"Set {AccountDetails.SpatialAnchorsAccountId} and {AccountDetails.SpatialAnchorsAccountKey} in {nameof(AccountDetails)}.cs\"", ToastLength.Long)
                        .Show();

                Finish();
                return;
            }

            if (string.IsNullOrEmpty(AccountDetails.AnchorSharingServiceUrl) || AccountDetails.AnchorSharingServiceUrl == "Set me")
            {
                Toast.MakeText(this, $"Set the {AccountDetails.AnchorSharingServiceUrl} in {nameof(AccountDetails)}.cs", ToastLength.Long)
                        .Show();

                Finish();
                return;
            }

            anchorSharingServiceClient = new AnchorSharingServiceClient(AccountDetails.AnchorSharingServiceUrl);

            UpdateStatic();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_shared);

            arFragment = (ArFragment)SupportFragmentManager.FindFragmentById(Resource.Id.ux_fragment);
            arFragment.TapArPlane += (sender, args) => OnTapArPlaneListener(args.HitResult, args.Plane, args.MotionEvent);

            sceneView = arFragment.ArSceneView;

            exitButton = (Button)FindViewById(Resource.Id.mainMenu);
            exitButton.Click += OnExitDemoClicked;
            exitButton.Visibility = ViewStates.Gone;
            textView = (TextView)FindViewById(Resource.Id.textView);
            textView.Visibility = ViewStates.Visible;
            locateButton = (Button)FindViewById(Resource.Id.locateButton);
            locateButton.Click += OnLocateButtonClicked;
            createButton = (Button)FindViewById(Resource.Id.createButton);
            createButton.Click += OnCreateButtonClicked;
            anchorNumInput = (EditText)FindViewById(Resource.Id.anchorNumText);
            editTextInfo = (TextView)FindViewById(Resource.Id.editTextInfo);
            backingPlateTwo = (LinearLayout) FindViewById(Resource.Id.backingplateTwo);

            currentStep = DemoStep.MindrStart;

            EnableCorrectUIControls();

            Scene scene = sceneView.Scene;
            scene.Update += (_, args) =>
            {
                // Pass frames to Spatial Anchors for processing.
                cloudAnchorManager?.Update(sceneView.ArFrame);
            };

            // Initialize the colors.
            MaterialFactory.MakeOpaqueWithColor(this, new Color(Android.Graphics.Color.Red)).GetAsync().ContinueWith(materialTask => failedColor = (Material)materialTask.Result);
            MaterialFactory.MakeOpaqueWithColor(this, new Color(Android.Graphics.Color.Green)).GetAsync().ContinueWith(materialTask => savedColor = (Material)materialTask.Result);
            MaterialFactory.MakeOpaqueWithColor(this, new Color(Android.Graphics.Color.DarkBlue)).GetAsync().ContinueWith(materialTask => selectedColor = (Material)materialTask.Result);
            MaterialFactory.MakeOpaqueWithColor(this, new Color(Android.Graphics.Color.Orange)).GetAsync().ContinueWith(materialTask =>
            {
                readyColor = (Material)materialTask.Result;
                foundColor = readyColor;
            });

            // HAX
            LocateAllAnchors();
        }

        public async void OnExitDemoClicked(object sender, EventArgs args)
        {
            //HAX
            LocateAllAnchors();
            return;

            lock (renderLock)
            {
                DestroySession();

                Finish();
            }
        }

        public async void LocateAllAnchors()
        {
            await Task.Delay(2000);

            textView.Text = "Searching for MindRs...";

            // clean up prev session just in case
            DestroySession();

            // start locating
            cloudAnchorManager = new AzureSpatialAnchorsManager(sceneView.Session);

            var anchorLocated = false;

            cloudAnchorManager.OnAnchorLocated += (sender, args) =>
                RunOnUiThread(async () =>
                {
                    CloudSpatialAnchor anchor = args.Anchor;
                    LocateAnchorStatus status = args.Status;

                    if (status == LocateAnchorStatus.AlreadyTracked || status == LocateAnchorStatus.Located)
                    {
                        AnchorVisual foundVisual = new AnchorVisual(anchor.LocalAnchor)
                        {
                            CloudAnchor = anchor
                        };

                        var mr = await _mindrService.TryGetContentsForAnchor(anchor.Identifier);
                        textView.Visibility = ViewStates.Visible;
                        textView.Text = mr != null ? mr.actualDesc : "No data found for MindR.";

                        foundVisual.AnchorNode.SetParent(arFragment.ArSceneView.Scene);
                        string cloudAnchorIdentifier = foundVisual.CloudAnchor.Identifier;
                        foundVisual.SetColor(foundColor);
                        foundVisual.AddToScene(arFragment, textView.Text);
                        anchorVisuals[cloudAnchorIdentifier] = foundVisual;


                        foundVisual.AnchorNode.SetOnTouchListener(this);
                        anchorLocated = true;
                    }
                });

            cloudAnchorManager.OnLocateAnchorsCompleted += (sender, args) =>
            {
                currentStep = DemoStep.MindrStart;

                RunOnUiThread(() =>
                {
                    textView.Text = anchorLocated ? "MindR(s) located!" : "Failed to find any MindRs.";

                    EnableCorrectUIControls();
                });
            };

            cloudAnchorManager.StartSession();

            await Task.Delay(2000);

            var ac = new AnchorLocateCriteria();
            var ids = await _mindrService.GetAllAnchorIds();
            ac.SetIdentifiers(ids.ToArray());
            cloudAnchorManager.StartLocating(ac);
        }

        public void OnCreateButtonClicked(object sender, EventArgs args)
        {
            if (currentStep == DemoStep.MindrStart)
            {
                DestroySession();

                currentStep = DemoStep.MindrName;
                textView.Text = "Name your MindR!";
                createButton.Text = "Set name";
                EnableCorrectUIControls();
            }

            if (currentStep == DemoStep.MindrName)
            {
                if (string.IsNullOrWhiteSpace(anchorNumInput.Text))
                {
                    textView.Text = "Please name your MindR";
                    return;
                }

                createButton.Text = "Save";

                textView.Text = "Scan your environment and place a MindR";
                DestroySession();

                cloudAnchorManager = new AzureSpatialAnchorsManager(sceneView.Session);

                cloudAnchorManager.OnSessionUpdated += (_, sessionUpdateArgs) =>
                {
                    SessionStatus status = sessionUpdateArgs.Status;

                    if (currentStep == DemoStep.MindrCreate)
                    {
                        float progress = status.RecommendedForCreateProgress;
                        if (progress >= 1.0)
                        {
                            if (anchorVisuals.TryGetValue(string.Empty, out AnchorVisual visual))
                            {
                                //Transition to saving...
                                TransitionToSaving(visual);
                            }
                            else
                            {
                                feedbackText = "Tap somewhere to place a MindR.";
                            }
                        }
                        else
                        {
                            feedbackText = $"Progress is {progress:0%}";
                        }
                    }
                };

                currentStep = DemoStep.MindrCreate;
                EnableCorrectUIControls();

                cloudAnchorManager.StartSession();
            }
        }

        private void AnchorPosted(string anchorNumber)
        {
            RunOnUiThread(() =>
            {
                textView.Text = "MindR saved, pasted url on clipboard";
                currentStep = DemoStep.MindrStart;
                cloudAnchorManager.StopSession();
                cloudAnchorManager = null;
                ClearVisuals();
                EnableCorrectUIControls();
                createButton.Text = "Create";
            });
        }

        private void ClearVisuals()
        {
            foreach (AnchorVisual visual in anchorVisuals.Values)
            {
                visual.Destroy();
            }

            anchorVisuals.Clear();
        }

        private Anchor CreateAnchor(HitResult hitResult)
        {
            AnchorVisual visual = new AnchorVisual(hitResult.CreateAnchor());
            visual.SetColor(readyColor);
            visual.AddToScene(arFragment);
            anchorVisuals[string.Empty] = visual;

            return visual.LocalAnchor;
        }

        private void CreateAnchorExceptionCompletion(string message)
        {
            textView.Text = message;
            currentStep = DemoStep.Start;
            cloudAnchorManager.StopSession();
            cloudAnchorManager = null;
            EnableCorrectUIControls();
        }

        private void DestroySession()
        {
            if (cloudAnchorManager != null)
            {
                cloudAnchorManager.StopSession();
                cloudAnchorManager = null;
            }

            StopWatcher();

            ClearVisuals();
        }

        private void EnableCorrectUIControls()
        {
            switch (currentStep)
            {
                case DemoStep.Start:
                    textView.Visibility = ViewStates.Visible;
                    locateButton.Visibility = ViewStates.Visible;
                    createButton.Visibility = ViewStates.Visible;
                    anchorNumInput.Visibility = ViewStates.Gone;
                    editTextInfo.Visibility = ViewStates.Gone;
                    SupportActionBar.Hide();
                    break;

                case DemoStep.CreateAnchor:
                    textView.Visibility = ViewStates.Visible;
                    locateButton.Visibility = ViewStates.Gone;
                    createButton.Visibility = ViewStates.Gone;
                    anchorNumInput.Visibility = ViewStates.Gone;
                    editTextInfo.Visibility = ViewStates.Gone;
                    break;

                case DemoStep.LocateAnchor:
                    textView.Visibility = ViewStates.Visible;
                    locateButton.Visibility = ViewStates.Gone;
                    createButton.Visibility = ViewStates.Gone;
                    anchorNumInput.Visibility = ViewStates.Gone;
                    editTextInfo.Visibility = ViewStates.Gone;
                    break;

                case DemoStep.SavingAnchor:
                    textView.Visibility = ViewStates.Visible;
                    locateButton.Visibility = ViewStates.Gone;
                    createButton.Visibility = ViewStates.Gone;
                    anchorNumInput.Visibility = ViewStates.Gone;
                    editTextInfo.Visibility = ViewStates.Gone;
                    break;

                case DemoStep.EnterAnchorNumber:
                    textView.Visibility = ViewStates.Visible;
                    locateButton.Visibility = ViewStates.Visible;
                    createButton.Visibility = ViewStates.Gone;
                    anchorNumInput.Visibility = ViewStates.Visible;
                    editTextInfo.Visibility = ViewStates.Visible;
                    break;

                case DemoStep.MindrStart:
                    textView.Visibility = ViewStates.Visible;
                    locateButton.Visibility = ViewStates.Gone;
                    createButton.Visibility = ViewStates.Visible;
                    anchorNumInput.Visibility = ViewStates.Gone;
                    editTextInfo.Visibility = ViewStates.Gone;
                    backingPlateTwo.Visibility = ViewStates.Gone;
                    break;

                case DemoStep.MindrName:
                    textView.Visibility = ViewStates.Visible;
                    locateButton.Visibility = ViewStates.Gone;
                    createButton.Visibility = ViewStates.Visible;
                    anchorNumInput.Visibility = ViewStates.Visible;
                    editTextInfo.Visibility = ViewStates.Visible;
                    backingPlateTwo.Visibility = ViewStates.Visible;
                    break;

                case DemoStep.MindrCreate:
                    textView.Visibility = ViewStates.Visible;
                    locateButton.Visibility = ViewStates.Gone;
                    createButton.Visibility = ViewStates.Gone;
                    anchorNumInput.Visibility = ViewStates.Gone;
                    editTextInfo.Visibility = ViewStates.Gone;
                    backingPlateTwo.Visibility = ViewStates.Gone;
                    break;

                case DemoStep.MindrSaving:
                    textView.Visibility = ViewStates.Visible;
                    locateButton.Visibility = ViewStates.Gone;
                    createButton.Visibility = ViewStates.Gone;
                    anchorNumInput.Visibility = ViewStates.Gone;
                    editTextInfo.Visibility = ViewStates.Gone;
                    backingPlateTwo.Visibility = ViewStates.Gone;
                    break;
            }
        }

        private void OnTapArPlaneListener(HitResult hitResult, Plane plane, MotionEvent motionEvent)
        {
            if (currentStep == DemoStep.MindrCreate)
            {
                if (!anchorVisuals.ContainsKey(string.Empty))
                {
                    CreateAnchor(hitResult);
                }
            }
        }

        private void StopWatcher()
        {
            if (cloudAnchorManager != null)
            {
                cloudAnchorManager.StopLocating();
            }
        }

        private void TransitionToSaving(AnchorVisual visual)
        {
            Log.Debug("ASADemo:", "transition to saving");
            currentStep = DemoStep.MindrSaving;
            EnableCorrectUIControls();
            Log.Debug("ASADemo", "creating anchor");
            CloudSpatialAnchor cloudAnchor = new CloudSpatialAnchor();
            visual.CloudAnchor = cloudAnchor;
            cloudAnchor.LocalAnchor = visual.LocalAnchor;

            cloudAnchorManager.CreateAnchorAsync(cloudAnchor)
                .ContinueWith(async cloudAnchorTask =>
                {
                    try
                    {
                        CloudSpatialAnchor anchor = await cloudAnchorTask;

                        string anchorId = anchor.Identifier;

                        Log.Debug("ASADemo:", "created anchor: " + anchorId);

                        Log.Debug("ASADemo", "recording anchor with web service");
                        Log.Debug("ASADemo", "anchorId: " + anchorId);

                        //SendAnchorResponse response = await this.anchorSharingServiceClient.SendAnchorIdAsync(anchorId);

                        var saveAnchorResult = await _mindrService.CreateAnchorAsync(anchorNumInput.Text, anchor.Identifier);
                        if (saveAnchorResult != null)
                        {
                            CrossClipboard.Current.SetText($"{MindrService.MindrService.BaseUrl}{saveAnchorResult.uri}");
                            visual.SetColor(savedColor);
                            AnchorPosted("");

                            await Task.Delay(1000);
                            RunOnUiThread(() =>
                            {
                                var shareIntent = new Intent(Android.Content.Intent.ActionSend);
                                shareIntent.SetType("text/plain");
                                shareIntent.PutExtra(Intent.ExtraText, $"{MindrService.MindrService.BaseUrl}{saveAnchorResult.uri}");
                                StartActivity(shareIntent);
                            });
                            await Task.Delay(1000);

                            RunOnUiThread(LocateAllAnchors);
                        }
                        else
                        {
                            visual.SetColor(failedColor);
                            await cloudAnchorManager.DeleteAnchorAsync(anchor);
                        }

                        anchorVisuals[anchorId] = visual;
                        anchorVisuals.TryRemove(string.Empty, out _);
                    }
                    catch (CloudSpatialException ex)
                    {
                        CreateAnchorExceptionCompletion($"{ex.Message}, {ex.ErrorCode}");
                    }
                    catch (Exception ex)
                    {
                        CreateAnchorExceptionCompletion(ex.Message);
                        visual.SetColor(failedColor);
                    }
                });
        }

        private void UpdateStatic()
        {
            new Handler().PostDelayed(() =>
            {
                switch (currentStep)
                {
                    case DemoStep.Start:
                        break;

                    case DemoStep.MindrCreate:
                        textView.Text = feedbackText;
                        break;

                    case DemoStep.LocateAnchor:
                        if (!string.IsNullOrEmpty(anchorId))
                        {
                            textView.Text = "searching for\n" + anchorId;
                        }
                        break;

                    case DemoStep.SavingAnchor:
                        textView.Text = "saving...";
                        break;

                    case DemoStep.EnterAnchorNumber:
                        break;
                }

                UpdateStatic();
            }, 500);
        }

        /* not used */

        public void OnLocateButtonClicked(object sender, EventArgs args)
        {
            if (currentStep == DemoStep.Start)
            {
                currentStep = DemoStep.EnterAnchorNumber;
                textView.Text = "Enter an anchor number and press locate";
                EnableCorrectUIControls();
            }
            else
            {
                string inputVal = anchorNumInput.Text;
                if (!string.IsNullOrEmpty(inputVal))
                {
                    Task.Run(async () =>
                    {
                        RetrieveAnchorResponse response = await anchorSharingServiceClient.RetrieveAnchorIdAsync(inputVal);

                        if (response.AnchorFound)
                        {
                            AnchorLookedUp(response.AnchorId);
                        }
                        else
                        {
                            RunOnUiThread(() => {
                                currentStep = DemoStep.Start;
                                EnableCorrectUIControls();
                                textView.Text = "Anchor number not found or has expired.";
                            });
                        }
                    });

                    currentStep = DemoStep.LocateAnchor;
                    EnableCorrectUIControls();
                }
            }
        }

        private void AnchorLookedUp(string anchorId)
        {
            Log.Debug("ASADemo", "anchor " + anchorId);
            this.anchorId = anchorId;
            DestroySession();

            bool anchorLocated = false;

            cloudAnchorManager = new AzureSpatialAnchorsManager(sceneView.Session);
            cloudAnchorManager.OnAnchorLocated += (sender, args) =>
                RunOnUiThread(() =>
                {
                    CloudSpatialAnchor anchor = args.Anchor;
                    LocateAnchorStatus status = args.Status;

                    if (status == LocateAnchorStatus.AlreadyTracked || status == LocateAnchorStatus.Located)
                    {
                        anchorLocated = true;

                        AnchorVisual foundVisual = new AnchorVisual(anchor.LocalAnchor)
                        {
                            CloudAnchor = anchor
                        };
                        foundVisual.AnchorNode.SetParent(arFragment.ArSceneView.Scene);
                        string cloudAnchorIdentifier = foundVisual.CloudAnchor.Identifier;
                        foundVisual.SetColor(foundColor);
                        foundVisual.AddToScene(arFragment);
                        anchorVisuals[cloudAnchorIdentifier] = foundVisual;
                    }
                });

            cloudAnchorManager.OnLocateAnchorsCompleted += (sender, args) =>
            {
                currentStep = DemoStep.Start;

                RunOnUiThread(() =>
                {
                    if (anchorLocated)
                    {
                        textView.Text = "Anchor located!";
                    }
                    else
                    {
                        textView.Text = "Anchor was not located. Check the logs for errors and\\or create a new anchor and try again.";
                    }

                    EnableCorrectUIControls();
                });
            };
            cloudAnchorManager.StartSession();
            AnchorLocateCriteria criteria = new AnchorLocateCriteria();
            criteria.SetIdentifiers(new string[] { anchorId });
            cloudAnchorManager.StartLocating(criteria);
        }

        private Node prevNode;

        public bool OnTouch(HitTestResult p0, MotionEvent p1)
        {
            if (p0?.Node?.Name == null) return false;

            if (prevNode != null)
            {
                var prenderable = prevNode.Renderable.MakeCopy();
                prenderable.SetMaterial(0, foundColor);
                prevNode.Renderable = prenderable;
            }

            prevNode = p0.Node;

            textView.Text = p0.Node.Name;

            var renderable = prevNode.Renderable.MakeCopy();
            renderable.SetMaterial(0, selectedColor);
            prevNode.Renderable = renderable;

            return true;
        }
    }
}