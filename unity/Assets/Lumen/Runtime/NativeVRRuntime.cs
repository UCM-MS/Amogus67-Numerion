using System;
using System.Collections;
using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Tpd = UnityEngine.InputSystem.XR.TrackedPoseDriver;

namespace Lumen.Runtime
{
    /// <summary>
    /// Optional seated/standing PC OpenXR. Activation requires a running display AND a real tracked HMD.
    /// Existing server-authoritative walking consumes Move/HeadYaw/ConsumeJump from LumenGame.FixedUpdate.
    /// Desktop camera code must skip updates while Active. No physical-headset QA has been performed.
    /// </summary>
    public sealed class NativeVRRuntime : MonoBehaviour
    {
        public bool Active { get; private set; }
        public bool Busy { get; private set; }
        public string Status { get; private set; } = "PC VR je pripravené na pripojenie cez OpenXR.";
        public event Action<bool> ActiveChanged;
        public Vector2 Move => Active && HeadTracked && !game.ModalOpen ? Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1) : Vector2.zero;
        public float HeadYaw => game.ViewCamera.transform.eulerAngles.y;
        public bool HeadTracked { get; private set; }
        public bool OwnsCamera => origin != null;

        private LumenGame game;
        private XRManagerSettings manager;
        private XROrigin origin;
        private GameObject offset;
        private Tpd headDriver;
        private Camera view;
        private Transform cameraParent;
        private Vector3 oldCameraPosition, oldCameraScale;
        private Quaternion oldCameraRotation;
        private float oldFov, oldNear;
        private StereoTargetEyeMask oldStereoTarget;
        private Vector3 originOffset;
        private InputActionAsset actions;
        private InputAction headPosition, headRotation, headState, move, turn, jump, menu, click, pointerPosition, pointerRotation, uiScroll;
        private bool jumpPending, snapReady = true, modalWasOpen;
        private float lostFor;
        private Canvas hudCanvas;
        private CanvasScaler hudScaler;
        private Transform hudParent;
        private Vector3 hudPosition, hudScale;
        private Quaternion hudRotation;
        private Vector2 hudSize;
        private CanvasScaler.ScaleMode scalerMode;
        private UnityEngine.RenderMode canvasMode;
        private Camera canvasCamera;
        private InputSystemUIInputModule ui;
        private InputActionReference oldClick, oldPointerPosition, oldPointerRotation, oldScroll;
        private Transform oldTrackingOrigin;
        private TrackedDeviceRaycaster xrRaycaster;
        private bool createdRaycaster;
        private LineRenderer pointerLine;
        private readonly List<UnityEngine.XR.InputDevice> headsets = new List<UnityEngine.XR.InputDevice>();
        private readonly List<InputActionReference> temporaryReferences = new List<InputActionReference>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ReserveStereoViews() { UnityEngine.Rendering.TextureXR.maxViews = 2; }

        public void Initialize(LumenGame owner)
        {
            game = owner; view = owner.ViewCamera;
            // Create and enable ALL XR actions before starting OpenXR. Bindings attach once per session.
            actions = ScriptableObject.CreateInstance<InputActionAsset>(); actions.name = "LUMEN Native XR";
            var map = actions.AddActionMap("LumenXR");
            headPosition = map.AddAction("HeadPosition", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlLayout: "Vector3");
            headRotation = map.AddAction("HeadRotation", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlLayout: "Quaternion");
            headState = map.AddAction("HeadState", InputActionType.Value, "<XRHMD>/trackingState", expectedControlLayout: "Integer");
            move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2", processors: "stickDeadzone");
            turn = map.AddAction("Turn", InputActionType.Value, expectedControlLayout: "Vector2", processors: "stickDeadzone");
            jump = map.AddAction("Jump", InputActionType.Button);
            menu = map.AddAction("Menu", InputActionType.Button);
            click = map.AddAction("UIClick", InputActionType.PassThrough, expectedControlLayout: "Button");
            pointerPosition = map.AddAction("PointerPosition", InputActionType.PassThrough, expectedControlLayout: "Vector3");
            pointerRotation = map.AddAction("PointerRotation", InputActionType.PassThrough, expectedControlLayout: "Quaternion");
            uiScroll = map.AddAction("UIScroll", InputActionType.PassThrough, expectedControlLayout: "Vector2", processors: "scaleVector2(x=0,y=20)");
            foreach (string layout in new[] { "OculusTouchController", "ValveIndexController", "WMRSpatialController" })
            {
                move.AddBinding("<" + layout + ">{LeftHand}/thumbstick");
                turn.AddBinding("<" + layout + ">{RightHand}/thumbstick");
                uiScroll.AddBinding("<" + layout + ">{RightHand}/thumbstick");
                jump.AddBinding("<" + layout + ">{RightHand}/primaryButton");
                menu.AddBinding("<" + layout + ">{RightHand}/secondaryButton");
                menu.AddBinding("<" + layout + ">{LeftHand}/menu");
                click.AddBinding("<" + layout + ">{RightHand}/triggerPressed");
                pointerPosition.AddBinding("<" + layout + ">{RightHand}/pointerPosition");
                pointerRotation.AddBinding("<" + layout + ">{RightHand}/pointerRotation");
            }
            move.AddBinding("<ViveController>{LeftHand}/trackpad");
            turn.AddBinding("<ViveController>{RightHand}/trackpad");
            menu.AddBinding("<ViveController>{LeftHand}/menu");
            jump.AddBinding("<ViveController>{RightHand}/trackpadClicked");
            jump.AddBinding("<WMRSpatialController>{RightHand}/joystickClicked");
            click.AddBinding("<ViveController>{RightHand}/triggerPressed");
            pointerPosition.AddBinding("<ViveController>{RightHand}/pointerPosition");
            pointerRotation.AddBinding("<ViveController>{RightHand}/pointerRotation");
            click.AddBinding("<KHRSimpleController>{RightHand}/select");
            menu.AddBinding("<KHRSimpleController>{LeftHand}/menu");
            pointerPosition.AddBinding("<KHRSimpleController>{RightHand}/pointerPosition");
            pointerRotation.AddBinding("<KHRSimpleController>{RightHand}/pointerRotation");
            actions.Enable();
        }

        public bool ConsumeJump() { bool value = jumpPending; jumpPending = false; return value; }

        public void StartXR()
        {
            if (Active || Busy || game == null) return;
            if (Application.platform != RuntimePlatform.WindowsPlayer && Application.platform != RuntimePlatform.WindowsEditor)
            { SetStatus("Tento VR režim je pripravený pre Windows PC a OpenXR headset."); return; }
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            { SetStatus("VR potrebuje grafický Windows prehrávač a pripojený OpenXR headset."); return; }
            manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
            if (manager == null || manager.activeLoaders.Count == 0)
            { SetStatus("OpenXR loader nie je nakonfigurovaný. V Editore spusti LUMEN / Prepare Optional PC VR."); return; }
            StartCoroutine(Enter());
        }

        private IEnumerator Enter()
        {
            Busy = true; SetStatus("Pripájam OpenXR headset…");
            yield return null; // manual XR initialization must occur after Start/graphics initialization.
            if (manager.activeLoader == null) yield return manager.InitializeLoader();
            if (manager.activeLoader == null)
            { Busy = false; SetStatus("OpenXR headset sa nepodarilo inicializovať. Skontroluj pripojenie a aktívny PC OpenXR runtime."); yield break; }
            manager.StartSubsystems();
            float until = Time.realtimeSinceStartup + 12;
            XRDisplaySubsystem display = manager.activeLoader.GetLoadedSubsystem<XRDisplaySubsystem>();
            while (Time.realtimeSinceStartup < until && (display == null || !display.running || !HasTrackedHeadset()))
            {
                yield return null;
                display = manager.activeLoader != null ? manager.activeLoader.GetLoadedSubsystem<XRDisplaySubsystem>() : null;
            }
            if (display == null || !display.running || !HasTrackedHeadset())
            { manager.StopSubsystems(); manager.DeinitializeLoader(); Busy = false; SetStatus("Aktívny sledovaný headset sa nenašiel. Hra pokračuje na monitore."); yield break; }
            game.Graphics.TrySetMode(Lumen.Graphics.RenderMode.Raster, out _);
            SaveCameraAndCreateRig();
            ConvertHudToWorldSpace();
            Active = true; Busy = false; HeadTracked = true; lostFor = 0;
            ActiveChanged?.Invoke(true);
            SetStatus("VR aktívne · " + OpenXRRuntime.name + " · ľavá páčka pohyb, pravá otočenie, A skok, B menu.");
            game.Hud.CloseModal();
        }

        private bool HasTrackedHeadset()
        {
            headsets.Clear();
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, headsets);
            foreach (var headset in headsets)
                if (headset.isValid && headset.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && tracked) return true;
            return false;
        }

        private void SaveCameraAndCreateRig()
        {
            cameraParent = view.transform.parent; oldCameraPosition = view.transform.localPosition;
            oldCameraRotation = view.transform.localRotation; oldCameraScale = view.transform.localScale;
            oldFov = view.fieldOfView; oldNear = view.nearClipPlane; oldStereoTarget = view.stereoTargetEye;
            float forwardYaw = view.transform.eulerAngles.y;
            var root = new GameObject("LUMEN OpenXR Origin"); root.transform.position = game.PlayerPosition;
            root.transform.rotation = Quaternion.Euler(0, forwardYaw, 0);
            offset = new GameObject("Camera floor offset"); offset.transform.SetParent(root.transform, false);
            view.transform.SetParent(offset.transform, false); view.transform.localPosition = Vector3.zero; view.transform.localRotation = Quaternion.identity;
            view.nearClipPlane = .05f; view.stereoTargetEye = StereoTargetEyeMask.Both;
            origin = root.AddComponent<XROrigin>(); origin.Origin = root; origin.Camera = view;
            origin.CameraFloorOffsetObject = offset; origin.CameraYOffset = 1.65f;
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            headDriver = view.gameObject.AddComponent<Tpd>(); headDriver.enabled = false;
            headDriver.trackingType = Tpd.TrackingType.RotationAndPosition;
            headDriver.updateType = Tpd.UpdateType.UpdateAndBeforeRender; headDriver.ignoreTrackingState = false;
            headDriver.positionInput = new InputActionProperty(Reference(headPosition));
            headDriver.rotationInput = new InputActionProperty(Reference(headRotation));
            headDriver.trackingStateInput = new InputActionProperty(Reference(headState));
            headDriver.enabled = true;
            originOffset = Vector3.zero;
            var line = new GameObject("VR menu pointer"); line.transform.SetParent(root.transform, false);
            pointerLine = line.AddComponent<LineRenderer>(); pointerLine.useWorldSpace = true; pointerLine.positionCount = 2;
            pointerLine.generateLightingData = true;
            pointerLine.startWidth = .006f; pointerLine.endWidth = .002f;
            pointerLine.sharedMaterial = Lumen.World.LumenMaterials.Get("xr-menu-ray", new Color(.7f,.6f,1), 0, 0);
            pointerLine.enabled = false;
        }

        private void ConvertHudToWorldSpace()
        {
            hudCanvas = game.Hud.GetComponentInChildren<Canvas>(true);
            if (hudCanvas == null) return;
            var rect = (RectTransform)hudCanvas.transform;
            hudParent = rect.parent; hudPosition = rect.localPosition; hudRotation = rect.localRotation; hudScale = rect.localScale; hudSize = rect.sizeDelta;
            canvasMode = hudCanvas.renderMode; canvasCamera = hudCanvas.worldCamera;
            hudScaler = hudCanvas.GetComponent<CanvasScaler>();
            if (hudScaler != null) { scalerMode = hudScaler.uiScaleMode; hudScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; }
            hudCanvas.renderMode = UnityEngine.RenderMode.WorldSpace; hudCanvas.worldCamera = view;
            rect.SetParent(origin.transform, false); rect.sizeDelta = new Vector2(1920, 1080); rect.localScale = Vector3.one * .0013f;
            xrRaycaster = hudCanvas.GetComponent<TrackedDeviceRaycaster>(); createdRaycaster = xrRaycaster == null;
            if (createdRaycaster) xrRaycaster = hudCanvas.gameObject.AddComponent<TrackedDeviceRaycaster>();
            xrRaycaster.maxDistance = 8; xrRaycaster.checkFor3DOcclusion = false;
            ui = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            if (ui != null)
            {
                oldClick = ui.leftClick; oldPointerPosition = ui.trackedDevicePosition; oldPointerRotation = ui.trackedDeviceOrientation;
                oldScroll = ui.scrollWheel; oldTrackingOrigin = ui.xrTrackingOrigin;
                ui.leftClick = Reference(click); ui.trackedDevicePosition = Reference(pointerPosition);
                ui.trackedDeviceOrientation = Reference(pointerRotation); ui.scrollWheel = Reference(uiScroll);
                ui.xrTrackingOrigin = offset.transform;
            }
            modalWasOpen = false;
        }

        private void Update()
        {
            if (!Active) return;
            HeadTracked = HasTrackedHeadset();
            var display = manager?.activeLoader?.GetLoadedSubsystem<XRDisplaySubsystem>();
            lostFor = HeadTracked && display != null && display.running ? 0 : lostFor + Time.unscaledDeltaTime;
            if (lostFor > 10) { StopXR(); SetStatus("Sledovanie headsetu sa prerušilo. Hra pokračuje na monitore."); return; }
            if (!HeadTracked) return;
            if (menu.WasPressedThisFrame()) game.Hud.ToggleMenu();
            if (!game.ModalOpen)
            {
                if (jump.WasPressedThisFrame()) jumpPending = true;
                if (click.WasPressedThisFrame()) game.Interact();
                float x = turn.ReadValue<Vector2>().x;
                if (Mathf.Abs(x) < .25f) snapReady = true;
                else if (snapReady && Mathf.Abs(x) > .65f)
                {
                    origin.RotateAroundCameraUsingOriginUp(Mathf.Sign(x) * 30);
                    originOffset = origin.transform.position - game.PlayerPosition; snapReady = false;
                }
            }
        }

        private void LateUpdate()
        {
            if (!Active || origin == null) return;
            origin.transform.position = game.PlayerPosition + originOffset;
            if (hudCanvas == null) return;
            bool visible = game.ModalOpen;
            if (pointerLine != null)
            {
                pointerLine.enabled = visible && HeadTracked && pointerPosition.activeControl != null;
                if (pointerLine.enabled)
                {
                    Vector3 start = offset.transform.TransformPoint(pointerPosition.ReadValue<Vector3>());
                    Quaternion rotation = offset.transform.rotation * pointerRotation.ReadValue<Quaternion>();
                    pointerLine.SetPosition(0, start); pointerLine.SetPosition(1, start + rotation * Vector3.forward * 3);
                }
            }
            if (visible && !modalWasOpen)
            {
                Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
                if (forward.sqrMagnitude < .1f) forward = origin.transform.forward;
                hudCanvas.transform.position = view.transform.position + forward * 2.2f - Vector3.up * .15f;
                hudCanvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }
            hudCanvas.enabled = visible; modalWasOpen = visible;
        }

        public void StopXR()
        {
            if (Busy) StopAllCoroutines();
            bool wasActive = Active; Active = false; Busy = false; HeadTracked = false; jumpPending = false;
            if (headDriver != null) { headDriver.enabled = false; Destroy(headDriver); }
            if (ui != null)
            {
                ui.leftClick = oldClick; ui.trackedDevicePosition = oldPointerPosition; ui.trackedDeviceOrientation = oldPointerRotation;
                ui.scrollWheel = oldScroll; ui.xrTrackingOrigin = oldTrackingOrigin;
            }
            if (hudCanvas != null)
            {
                hudCanvas.enabled = true;
                if (createdRaycaster && xrRaycaster != null) { xrRaycaster.enabled = false; Destroy(xrRaycaster); }
                hudCanvas.renderMode = canvasMode; hudCanvas.worldCamera = canvasCamera;
                var rect = (RectTransform)hudCanvas.transform; rect.SetParent(hudParent, false);
                rect.localPosition = hudPosition; rect.localRotation = hudRotation; rect.localScale = hudScale; rect.sizeDelta = hudSize;
                if (hudScaler != null) hudScaler.uiScaleMode = scalerMode;
            }
            if (origin != null)
            {
                if (view != null)
                {
                    view.transform.SetParent(cameraParent, false); view.transform.localPosition = oldCameraPosition;
                    view.transform.localRotation = oldCameraRotation; view.transform.localScale = oldCameraScale;
                    view.fieldOfView = oldFov; view.nearClipPlane = oldNear;
                    view.stereoTargetEye = oldStereoTarget;
                }
                Destroy(origin.gameObject); origin = null;
            }
            foreach (var reference in temporaryReferences) if (reference != null) Destroy(reference);
            temporaryReferences.Clear();
            if (manager != null && manager.activeLoader != null) { manager.StopSubsystems(); manager.DeinitializeLoader(); }
            if (wasActive) ActiveChanged?.Invoke(false);
            if (game != null) SetStatus("VR ukončené · hráš na monitore.");
            hudCanvas = null; ui = null;
        }

        private void SetStatus(string value) { Status = value; if (game != null) game.Notify(value); }
        private InputActionReference Reference(InputAction action)
        {
            var reference = InputActionReference.Create(action); temporaryReferences.Add(reference); return reference;
        }
        private void OnDestroy() { StopXR(); if (actions != null) { actions.Disable(); Destroy(actions); } }
    }
}
