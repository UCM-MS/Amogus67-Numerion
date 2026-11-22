using System;
using System.Collections.Generic;
using System.Linq;
using Lumen.Core;
using Lumen.Networking;
using Lumen.World;
using UnityEngine;
using UnityEngine.InputSystem;
using RenderMode = Lumen.Graphics.RenderMode;

namespace Lumen.Runtime
{
    public sealed partial class LumenGame : MonoBehaviour
    {
        public GameObject ExplorerPrefab;
        public GameSave Save { get; private set; }
        public PlayerProfile Profile => Save.Profile;
        public LearningService Learning { get; private set; }
        public ContentCatalog Catalog { get; private set; }
        public NetworkSession Session { get; private set; }
        public Lumen.Graphics.GraphicsController Graphics { get; private set; }
        public LumenHud Hud { get; private set; }
        public NativeVRRuntime VR { get; private set; }
        public Camera ViewCamera { get; private set; }
        public bool ModalOpen => Hud != null && Hud.ModalOpen;
        public bool Connected => Session != null && Session.LocalPlayer != null;
        public Vector3 PlayerPosition => Connected ? Session.LocalPlayer.transform.position : offline.transform.position;
        public float PlayerYaw => Connected ? Session.LocalPlayer.transform.eulerAngles.y : offline != null ? offline.transform.eulerAngles.y : yaw;
        public InteractiveStation Target { get; private set; }
        public bool PhotoMode => Graphics != null && Graphics.IsPhotoMode;
        public float CourseSeconds { get; private set; }
        public int CourseCheckpoint { get; private set; }
        public bool CourseActive { get; private set; }
        public string Toast { get; private set; } = "Numerion · ďalší cieľ nájdeš v ponuke.";
        public bool OnlineQuestion { get; private set; }
        public Question CurrentQuestion { get; private set; }
        public Subject CurrentSubject { get; private set; }
        private WorldLayout world;
        private GameObject offline;
        private CharacterController offlineController;
        private AvatarVisual offlineAvatar;
        private Transform pet;
        private Vector3 offlineVelocity;
        private float yaw = 180, pitch = 24, cameraDistance = 6.5f, saveTimer, toastUntil, courseFall;
        private bool headless;
        private bool jumpQueued, wasOnline;
        private int pendingOnlineChoice = -1;
        private float onlineQuestionDeadline;
        private Vector3 tutorialLastPosition;
        private float tutorialJumpUntil, tutorialJumpY;
        private Vector3 lastOnlinePosition;
        private float nextSharedBeaconRequest;
        private readonly Dictionary<NetworkExplorer, AvatarVisual> peers = new Dictionary<NetworkExplorer, AvatarVisual>();
        private readonly HashSet<string> acceptedNetworkAnswers = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBoot()
        {
            if (FindFirstObjectByType<LumenGame>() == null)
            {
                var go = new GameObject("Numerion Academy");
                go.AddComponent<LumenGame>();
            }
        }

        private void Awake()
        {
            headless = Application.isBatchMode && Environment.GetCommandLineArgs().Any(a => a == "-lumen-server" || a == "-lumen-smoke");
            Save = headless ? new GameSave() : ProfileStore.Load(); Save.Options ??= new GameOptions(); Save.Options.Normalize(); NativePerformance.Apply(Save);
            if (!headless && !Application.isEditor && Display.main.systemWidth > 0)
                Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
            Time.fixedDeltaTime = 1f / 60; Time.maximumDeltaTime = .15f;
            Catalog = ContentCatalog.CreateDefault(); Learning = new LearningService(Catalog);
            world = AcademyWorld.Build(headless);
            Session = new GameObject("LUMEN Network · 60 Hz").AddComponent<NetworkSession>();
            ExplorerPrefab ??= Resources.Load<GameObject>("Generated/Explorer");
            if (ExplorerPrefab != null) Session.Configure(ExplorerPrefab, 60, world.SpawnPosition);
            else Debug.LogError("Chýba sieťový prefab. Spusti Numerion > Prepare Academy v Editore.");
            var beacons = world.Collectibles.Where(c => c.IsBeacon).ToArray();
            if (beacons.Length > 0) Session.ConfigureBeacons(beacons.Select(c => c.transform.position).ToArray());
            if (world.ParkourPlatforms.Count > 0) Session.ConfigureCourse(world.ParkourPlatforms.ToArray());
            Session.LocalPlayerReady += AttachLocalPlayer;
            Session.StatusChanged += Notify;
            Session.BeaconCollected += (index, name, total) => Notify($"{name} rozsvietil maják · {total / 10}/{beacons.Length}");
            NetworkExplorer.Spawned += AddPeer;
            NetworkExplorer.Despawned += RemovePeer;
            if (headless) { Session.TryStartFromCommandLine(); return; }

            offline = new GameObject("Solo explorer"); offline.transform.position = world.SpawnPosition;
            offline.transform.rotation = Quaternion.Euler(0, yaw, 0);
            offlineController = offline.AddComponent<CharacterController>();
            ExplorerPhysics.Configure(offlineController);
            offlineAvatar = AvatarFactory.Create(Profile.DisplayName, Appearance());
            offlineAvatar.transform.SetParent(offline.transform, false);
            offlineAvatar.SetNameVisible(false);
            var cameraGo = new GameObject("Lumen Camera"); cameraGo.tag = "MainCamera";
            ViewCamera = cameraGo.AddComponent<Camera>(); ViewCamera.fieldOfView = Save.Options.FieldOfView;
            ViewCamera.nearClipPlane = .07f; ViewCamera.farClipPlane = 450;
            cameraGo.AddComponent<AudioListener>();
            Graphics = Lumen.Graphics.GraphicsController.Create(ViewCamera);
            Graphics.PhotoModeChanged += active => { if (active) Notify("Fotorežim · nepohybuj kamerou, kým sa obraz vyčistí. Esc otvorí menu."); };
            Hud = gameObject.AddComponent<LumenHud>(); Hud.Initialize(this);
            VR = gameObject.AddComponent<NativeVRRuntime>(); VR.Initialize(this);
            InitializeSharedAcademy();
            InitializeOptions();
            toastUntil = Time.unscaledTime + 7;
            VR.ActiveChanged += active => {
                offlineAvatar?.SetVisible(!active);
                foreach (var pair in peers) if (pair.Key != null && pair.Key.IsOwner && pair.Value != null) pair.Value.SetVisible(!active);
            };
            UpdateCamera(1);
            tutorialLastPosition = world.SpawnPosition;
            if (!Environment.GetCommandLineArgs().Any(a => a.StartsWith("-lumen-smoke") || a == "-lumen-graphics-smoke" || a == "-lumen-skip-menu" || a == "-lumen-host" || a == "-lumen-connect")) Hud.ShowTitle();
            Session.TryStartFromCommandLine();
        }

        private void Update()
        {
            if (headless) return;
            float dt = Mathf.Min(Time.deltaTime, .1f);
            var keyboard = Keyboard.current;
            if (onlineQuestionDeadline > 0 && Time.unscaledTime > onlineQuestionDeadline) { onlineQuestionDeadline = 0; Hud.CloseModal(); Notify("Server neodpovedá. Otázku môžeš otvoriť znova; odmeny sa neduplikujú."); }
            if (Connected) { lastOnlinePosition = Session.LocalPlayer.transform.position; wasOnline = true; }
            else if (wasOnline) { wasOnline = false; offline.SetActive(true); TeleportOffline(lastOnlinePosition); CourseActive = false; Notify("Spojenie sa ukončilo. Pokračuješ v sólo svete."); }
            if (!ModalOpen && !PhotoMode) jumpQueued |= keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) Hud.ToggleMenu();
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame && (!ModalOpen || Hud.FeaturePanel == "MAP")) Hud.ToggleMap();
            if (keyboard != null && !ModalOpen && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)) Hud.ShowChat();
            if (!ModalOpen && keyboard != null)
            {
                if (keyboard.eKey.wasPressedThisFrame) Interact();
                if (keyboard.kKey.wasPressedThisFrame) Hud.ShowCurriculum();
                if (keyboard.jKey.wasPressedThisFrame) Hud.ShowJournal();
            }
            var mouse = Mouse.current;
            if (!ModalOpen && !(VR != null && VR.OwnsCamera) && mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue(); yaw += delta.x * .16f * Save.Options.MouseSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * .12f * Save.Options.MouseSensitivity * (Save.Options.InvertCamera ? -1 : 1), 8, 65);
            }
            if (!ModalOpen && mouse != null) cameraDistance = Mathf.Clamp(cameraDistance - mouse.scroll.ReadValue().y * .004f, 3, 11);
            if (Gamepad.current != null && !ModalOpen)
            {
                if (!(VR != null && VR.OwnsCamera)) {
                    var look = Gamepad.current.rightStick.ReadValue(); yaw += look.x * 125 * dt;
                    pitch = Mathf.Clamp(pitch - look.y * 80 * dt * (Save.Options.InvertCamera ? -1 : 1), 8, 65);
                }
                if (Gamepad.current.buttonWest.wasPressedThisFrame) Interact();
                if (!PhotoMode) jumpQueued |= Gamepad.current.buttonSouth.wasPressedThisFrame;
            }
            if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame) Hud.ToggleMenu();
            WorldAnimation.Frozen = PhotoMode || Save.Options.ReduceMotion;
            UpdateWorld(dt);
            UpdateSharedAcademy(dt);
            if (!PhotoMode && !(VR != null && VR.OwnsCamera)) UpdateCamera(dt);
            if (toastUntil > 0 && Time.unscaledTime > toastUntil) { Toast = ""; toastUntil = 0; }
            saveTimer += dt; if (saveTimer > 20) { saveTimer = 0; ProfileStore.Save(Save); }
            Hud.Refresh();
        }

        private void FixedUpdate()
        {
            if (headless || offline == null) return;
            if (!Connected && !offline.activeSelf) { offline.SetActive(true); TeleportOffline(wasOnline ? lastOnlinePosition : world.SpawnPosition); }
            if (PhotoMode) { if (Connected) Session.LocalPlayer.SetInput(Vector2.zero, yaw, false); jumpQueued = false; return; }
            Vector2 move = Vector2.zero; bool jump = false, sprint = false;
            bool xrJump = VR != null && VR.ConsumeJump();
            if (!ModalOpen && !PhotoMode)
            {
                var k = Keyboard.current;
                if (k != null)
                {
                    move = new Vector2((k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0),
                        (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0));
                    sprint = k.leftShiftKey.isPressed;
                }
                if (Gamepad.current != null) { move += Gamepad.current.leftStick.ReadValue(); sprint |= Gamepad.current.leftStickButton.isPressed; }
                jump = jumpQueued;
                if (VR != null && VR.Active) {
                    move += VR.Move; yaw = VR.HeadYaw; jump |= xrJump; sprint = false;
                    if (!VR.HeadTracked) { move = Vector2.zero; jump = false; }
                }
            }
            jumpQueued = false;
            move = Vector2.ClampMagnitude(move, 1);
            if (jump && Save.Tutorial.Active) { tutorialJumpUntil = Time.unscaledTime + 1.5f; tutorialJumpY = PlayerPosition.y; }
            if (Connected) { Session.LocalPlayer.SetInput(move, yaw, jump, sprint); return; }
            bool grounded = offlineController.isGrounded;
            if (grounded && offlineVelocity.y < 0) offlineVelocity.y = -2;
            if (jump && grounded) offlineVelocity.y = 7;
            offlineVelocity.y = Mathf.Max(ExplorerPhysics.TerminalSpeed, offlineVelocity.y - 22 * Time.fixedDeltaTime);
            Vector3 forward = Quaternion.Euler(0, yaw, 0) * new Vector3(move.x, 0, move.y);
            var contacts = offlineController.Move((forward * (sprint ? 7.8f : 5.4f) + offlineVelocity) * Time.fixedDeltaTime);
            offlineVelocity.y = ExplorerPhysics.ResolveVerticalSpeed(offlineVelocity.y, contacts, offlineController.isGrounded);
            if (forward.sqrMagnitude > .01f) offline.transform.rotation = Quaternion.Slerp(offline.transform.rotation, Quaternion.LookRotation(forward), .2f);
            if (offline.transform.position.y < -12)
            {
                if (SharedConnected) RequestSharedRespawn("spawn", -1, world.SpawnPosition);
                else TeleportOffline(world.SpawnPosition);
            }
            if (!PhotoMode) offlineAvatar.Tick(Time.fixedDeltaTime, move.sqrMagnitude > .01f);
        }

        private void UpdateCamera(float dt)
        {
            var pivot = PlayerPosition + Vector3.up * 1.35f;
            Vector3 offset = Quaternion.Euler(pitch, yaw, 0) * new Vector3(0, 0, -cameraDistance);
            float distance = offset.magnitude;
            if (Physics.SphereCast(pivot, .2f, offset.normalized, out var hit, distance, ~0, QueryTriggerInteraction.Ignore))
            { if (!hit.transform.IsChildOf(Connected ? Session.LocalPlayer.transform : offline.transform)) distance = Mathf.Max(.75f, hit.distance - .15f); }
            Vector3 desired = pivot + offset.normalized * distance;
            ViewCamera.transform.position = Vector3.Lerp(ViewCamera.transform.position, desired, 1 - Mathf.Exp(-dt * 12));
            ViewCamera.transform.LookAt(pivot);
        }

        private void UpdateWorld(float dt)
        {
            Vector3 position = PlayerPosition;
            if (Save.Tutorial.Active && !ModalOpen && !PhotoMode)
            {
                float distance = Vector2.Distance(new Vector2(position.x, position.z), new Vector2(tutorialLastPosition.x, tutorialLastPosition.z));
                if (distance < 2) Save.Tutorial.Distance = Mathf.Min(8, Save.Tutorial.Distance + distance);
                Save.Tutorial.Walked |= Save.Tutorial.Distance >= 8;
                if (Time.unscaledTime < tutorialJumpUntil && position.y > tutorialJumpY + .25f) Save.Tutorial.Jumped = true;
                CheckTutorialCompletion();
            }
            tutorialLastPosition = position;
            Target = null; float closest = 4;
            foreach (var station in world.Stations)
            {
                float d = Vector3.Distance(position, station.transform.position);
                if (d < station.InteractionRadius && d < closest) { Target = station; closest = d; }
            }
            int beaconIndex = 0;
            foreach (var item in world.Collectibles)
            {
                if (item.IsBeacon)
                {
                    bool lit = SharedConnected ? (Online.Room.beaconsMask & (1 << beaconIndex)) != 0
                        : Connected && (Session.LocalPlayer.SharedBeaconsMask & (1UL << beaconIndex)) != 0;
                    item.SetCollected(lit);
                    if (!lit && (Connected || SharedConnected) && !ModalOpen && !PhotoMode && Vector3.Distance(position, item.transform.position) < 2.4f)
                    {
                        if (SharedConnected && Time.unscaledTime >= nextSharedBeaconRequest && !Online.Busy)
                        { nextSharedBeaconRequest = Time.unscaledTime + 1; Online.CollectBeacon(beaconIndex); }
                        else if (Connected) Session.LocalPlayer.TryCollectBeacon(beaconIndex);
                    }
                    beaconIndex++;
                }
                else if (!item.Collected && !ModalOpen && !PhotoMode && Vector3.Distance(position + Vector3.up, item.transform.position) < 1.6f)
                {
                    item.SetCollected(true);
                    if (Save.Collected.Contains(item.Id)) continue;
                    Save.Collected.Add(item.Id); Profile.Progress.XP += 10; Profile.Progress.Coins += item.Value;
                    Profile.Progress.PetXP += 2; Notify($"Prieskum · +10 XP · +{item.Value} mincí"); ProfileStore.Save(Save);
                }
                else if (!item.IsBeacon && Save.Collected.Contains(item.Id)) item.SetCollected(true);
            }
            foreach (var pair in peers)
                if (pair.Key != null && pair.Value != null && !PhotoMode) pair.Value.Tick(dt, pair.Key.Speed > .1f);
            if (CourseActive && (Connected || (!ModalOpen && !PhotoMode)))
            {
                CourseSeconds += dt;
                if (Connected) CourseCheckpoint = Session.LocalPlayer.CourseCheckpoint;
                else if (SharedConnected)
                {
                    var self = Online.Room.players.FirstOrDefault(peer => peer.id == Online.IdentityId);
                    if (self != null) CourseCheckpoint = Mathf.Max(0, self.courseCheckpoint);
                }
                else if (CourseCheckpoint + 1 < world.ParkourPlatforms.Count && Vector3.Distance(position, world.ParkourPlatforms[CourseCheckpoint + 1]) < 1.3f)
                    CourseCheckpoint++;
                if (!Connected && CourseCheckpoint == world.ParkourPlatforms.Count - 1) FinishCourse(CourseSeconds);
                if (position.y < -3 && !Connected) { courseFall += dt; if (courseFall > .3f) { CourseRespawn(); courseFall = 0; } }
            }
        }

        private void AddPeer(NetworkExplorer explorer)
        {
            if (headless) return;
            var avatar = AvatarFactory.Create(explorer.DisplayName, explorer.IsOwner ? Appearance() : Appearance(explorer.Appearance));
            avatar.transform.SetParent(explorer.transform, false); avatar.SetNameVisible(!explorer.IsOwner && Save.Options.ShowNames);
            peers[explorer] = avatar;
            if (explorer.IsOwner && VR != null && VR.Active) avatar.SetVisible(false);
            explorer.AppearanceChanged += () => { if (avatar != null) { avatar.SetName(explorer.DisplayName); avatar.ApplyAppearance(explorer.IsOwner ? Appearance() : Appearance(explorer.Appearance)); } };
        }
        private void RemovePeer(NetworkExplorer explorer) { if (peers.TryGetValue(explorer, out var avatar) && avatar != null) Destroy(avatar.gameObject); peers.Remove(explorer); }
        private void AttachLocalPlayer(NetworkExplorer explorer)
        {
            if (headless) return;
            offline?.SetActive(false);
            explorer.QuestionReceived += q => { onlineQuestionDeadline = 0; CurrentQuestion = q; OnlineQuestion = true; Hud.ShowQuestion(q); };
            explorer.QuizAnswerReceived += result =>
            {
                onlineQuestionDeadline = 0;
                bool freshAnswer = result.Accepted && CurrentQuestion != null && acceptedNetworkAnswers.Add(CurrentQuestion.Id);
                if (freshAnswer)
                {
                    Profile.Progress.ActiveQuestionId = CurrentQuestion.Id;
                    Profile.Progress.ActiveQuestionIsPractice = result.Practice || Profile.Progress.Attempts.Any(a => a.QuestionId == CurrentQuestion.Id);
                    var localResult = Learning.SubmitAnswer(Profile, CurrentQuestion.Id, pendingOnlineChoice, DateTime.UtcNow);
                    if (localResult.Accepted && !localResult.Practice)
                    {
                        Profile.Progress.XP += result.XpAwarded - localResult.XpAwarded;
                        Profile.Progress.Coins += result.CoinsAwarded - localResult.CoinsAwarded;
                        Profile.Progress.PetXP += result.PetXpAwarded - localResult.PetXpAwarded;
                        var attempt = Profile.Progress.Attempts.Last(); attempt.XpAwarded = result.XpAwarded;
                        attempt.CoinsAwarded = result.CoinsAwarded; attempt.PetXpAwarded = result.PetXpAwarded;
                    }
                    else if (localResult.Practice) { result.Practice = true; result.XpAwarded = result.CoinsAwarded = result.PetXpAwarded = 0; }
                    ProfileStore.Save(Save);
                }
                Hud.ShowAnswer(result);
                if (freshAnswer) RewardCompanionAnswer(result);
            };
            explorer.QuizError += message => { onlineQuestionDeadline = 0; Hud.CloseModal(); Notify(message); };
            explorer.CourseCompleted += FinishCourse;
        }
        public void Disconnect()
        {
            Vector3 position = PlayerPosition; Session.Disconnect(); offline.SetActive(true); TeleportOffline(position);
            CourseActive = false; Notify("Pokračuješ v sólo svete.");
        }
        public void Interact()
        {
            if (PhotoMode) { Hud.ShowGraphics(); return; }
            if (Target == null) { Notify("Priblíž sa ku kiosku alebo otvor mapu klávesom M."); return; }
            switch (Target.Mode)
            {
                case "parkour": StartCourse(); return;
                case "pets": Hud.ShowPets(); return;
                case "customize": Hud.ShowProfile(false); return;
                case "coop": Hud.ShowMultiplayer(); return;
                case "expedition": InteractWithExpeditionStation(); return;
                case "arena": Hud.ShowArena(); return;
                case "npc": Hud.ShowNpc(Target.Id); return;
                case "study": Hud.ShowStudy(); return;
                case "artifacts": Hud.ShowArtifacts(); return;
                default: BeginQuiz(ParseSubject(Target.Subject)); return;
            }
        }
        public void BeginQuiz(Subject subject)
        {
            if (Save.Tutorial.Active) Save.Tutorial.OpenedQuiz = true;
            CurrentSubject = subject; OnlineQuestion = Connected;
            if (OnlineQuestion) { onlineQuestionDeadline = Time.unscaledTime + 8; Hud.ShowWaiting("Hostiteľ pripravuje otázku…"); Session.LocalPlayer.BeginQuiz(subject, Profile.AgeBand, Profile.Grade); }
            else { CurrentQuestion = Learning.BeginQuestion(Profile, subject, DateTime.UtcNow, true); Hud.ShowQuestion(CurrentQuestion); }
        }
        public void Answer(int option)
        {
            if (CurrentQuestion == null) return;
            if (OnlineQuestion && Connected) { pendingOnlineChoice = option; onlineQuestionDeadline = Time.unscaledTime + 8; Hud.ShowWaiting("Vyhodnocujeme odpoveď…"); Session.LocalPlayer.SubmitQuizAnswer(CurrentQuestion.Id, option); }
            else { var result = Learning.SubmitAnswer(Profile, CurrentQuestion.Id, option, DateTime.UtcNow); RewardCompanionAnswer(result); ProfileStore.Save(Save); Hud.ShowAnswer(result); }
        }
        public void ApplyProfile(string name, AgeBand age, int skin, int outfit, int hair, int accessory, int grade = 7)
        {
            Profile.DisplayName = ProfileRules.CleanName(name); Profile.AgeBand = age;
            Profile.Grade = CurriculumLabels.NormalizeGrade(grade);
            Profile.Appearance.SkinTone = skin; Profile.Appearance.OutfitColor = outfit;
            Profile.Appearance.HairStyle = hair; Profile.Appearance.Accessory = accessory;
            ProfileRules.Normalize(Profile); Save.HasCreatedProfile = true;
            offlineAvatar.SetName(Profile.DisplayName); offlineAvatar.ApplyAppearance(Appearance());
            Session.SendAppearance(Profile.DisplayName, NetworkLook()); ProfileStore.Save(Save);
            Notify("Tvoja postava je pripravená. Vyraz objavovať.");
        }
        public AvatarAppearance Appearance()
        {
            var appearance = Appearance(NetworkLook()); var equipped = Save.Companions.Equipped;
            appearance.Pet = equipped == null ? 0 : CompanionRules.Find(equipped.SpeciesId).Visual;
            appearance.PetGear = equipped?.Gear ?? 0; return appearance;
        }
        private AvatarAppearance Appearance(NetworkAppearance a) => new AvatarAppearance { SkinIndex = a.SkinTone, ColorIndex = a.OutfitColor, Hair = a.HairStyle, Hat = a.Accessory == 3 ? 2 : a.Accessory == 1 ? 1 : 0, Backpack = a.Accessory == 2 ? 1 : 0, Pet = 0 };
        public NetworkAppearance NetworkLook() => new NetworkAppearance(Profile.Appearance.SkinTone, Profile.Appearance.OutfitColor, Profile.Appearance.HairStyle, Profile.Appearance.Accessory);
        public void StartCourse()
        {
            if (world.ParkourPlatforms.Count < 2) return;
            Hud.CloseModal(); CourseActive = true; CourseSeconds = 0; CourseCheckpoint = 0;
            if (Connected) Session.LocalPlayer.StartCourse();
            else if (SharedConnected) RequestSharedRespawn("course", 0, world.ParkourPlatforms[0] + Vector3.up * .1f);
            else TeleportOffline(world.ParkourPlatforms[0] + Vector3.up * .1f);
            Notify("Nebeská dráha · preskoč plošiny po poradí. R v menu vráti kontrolný bod.");
        }
        public void CourseRespawn()
        {
            if (!CourseActive) return;
            if (Connected) Session.LocalPlayer.ReturnToCheckpoint(CourseCheckpoint);
            else if (SharedConnected) RequestSharedRespawn("checkpoint", CourseCheckpoint,
                world.ParkourPlatforms[Mathf.Clamp(CourseCheckpoint, 0, world.ParkourPlatforms.Count - 1)] + Vector3.up * .1f);
            else TeleportOffline(world.ParkourPlatforms[Mathf.Clamp(CourseCheckpoint, 0, world.ParkourPlatforms.Count - 1)] + Vector3.up * .1f);
        }
        private void FinishCourse(float seconds)
        {
            if (!CourseActive) return; CourseActive = false;
            bool record = Save.ParkourBest <= 0 || seconds < Save.ParkourBest;
            if (record) Save.ParkourBest = seconds;
            if (!Save.Purchases.Contains("first-course")) { Save.Purchases.Add("first-course"); Profile.Progress.Coins += 100; Profile.Progress.XP += 60; Save.Companions.Eggs++; }
            ProfileStore.Save(Save); Notify($"Dráha dokončená za {seconds:0.0} s{(record ? " · nový rekord!" : "")}");
        }
        public void TrainPet()
        {
            var pet = Save.Companions.Equipped;
            if (pet == null) { Notify("Najprv vezmi spoločníka na výpravu."); return; }
            if (Profile.Progress.Coins < CompanionRules.TrainingCost) { Notify("Na tréning potrebuješ 75 mincí."); return; }
            Profile.Progress.Coins -= CompanionRules.TrainingCost; Profile.Progress.PetXP += 60; pet.XP += 60; pet.Bond++;
            RefreshCompanion(); Hud.ShowPets(); Notify(pet.Nickname + " · +60 XP a silnejšie puto.");
        }
        public void EnterAcademy(bool guided = false)
        {
            if (!Save.HasCreatedProfile) { Hud.ShowProfile(true); return; }
            Hud.CloseModal();
            if (guided || (!Save.Tutorial.Complete && !Save.Purchases.Contains("tutorial-dismissed"))) Save.Tutorial.Active = true;
            tutorialLastPosition = PlayerPosition; ProfileStore.Save(Save);
        }
        public void ResumeTutorial()
        {
            if (Save.Tutorial.Complete) { Hud.ShowTutorial(); return; }
            Save.Tutorial.Active = true; EnterAcademy(true); Notify("Sprievodca pokračuje tam, kde si skončil.");
        }
        public void SuspendTutorial()
        {
            Save.Tutorial.Active = false;
            if (!Save.Purchases.Contains("tutorial-dismissed")) Save.Purchases.Add("tutorial-dismissed");
            ProfileStore.Save(Save); Notify("Sprievodcu kedykoľvek obnovíš v menu.");
        }
        public void RestartTutorial()
        {
            bool rewarded = Save.Tutorial.RewardClaimed;
            Save.Tutorial = new AcademyTutorial { Active = true, RewardClaimed = rewarded,
                ClaimedPet = Save.Companions.StarterClaimed, EquippedPet = Save.Companions.Equipped != null };
            EnterAcademy(true); Notify("Sprievodca sa začína od pohybu. Odmenu možno získať iba raz.");
        }
        private void CheckTutorialCompletion()
        {
            if (!Save.Tutorial.Complete) return;
            Save.Tutorial.Active = false;
            if (Save.Tutorial.RewardClaimed) return;
            Save.Tutorial.RewardClaimed = true;
            Profile.Progress.Coins += 120; Profile.Progress.XP += 20; Save.Companions.Eggs++;
            ProfileStore.Save(Save); Notify("Úvod zvládnutý! +120 mincí · +20 XP · 1 vajíčko pre zbierku.");
        }
        private void RewardCompanionAnswer(AnswerResult result)
        {
            if (!result.Accepted) return;
            Audio?.Answer(result.Correct);
            if (!result.Correct) return;
            if (Save.Tutorial.Active) Save.Tutorial.AnsweredCorrectly = true;
            if (!result.Practice)
            {
                GrantArtifact("question:" + Profile.Progress.Attempts.LastOrDefault()?.QuestionId, CurrentSubject);
                if (CompanionRules.RewardCorrectAnswer(Save.Companions, result.PetXpAwarded)) Notify("Tri správne odpovede · získavaš vajíčko pre spoločníka!");
                RefreshCompanion();
            }
            CheckTutorialCompletion(); ProfileStore.Save(Save);
        }
        public void ClaimStarter(string species)
        {
            var pet = CompanionRules.ClaimStarter(Save.Companions, species);
            if (pet == null) return;
            pet.XP = Mathf.Max(0, Profile.Progress.PetXP);
            Save.Tutorial.ClaimedPet = true; ProfileStore.Save(Save); Hud.ShowPets(); Notify(pet.Nickname + " sa pripája do tvojej zbierky. Vezmi ho na výpravu.");
        }
        public void EquipCompanion(string species)
        {
            if (!CompanionRules.Equip(Save.Companions, species)) return;
            Save.Tutorial.EquippedPet = true; RefreshCompanion(); CheckTutorialCompletion(); Hud.ShowPets();
            Notify(Save.Companions.Equipped.Nickname + " ide s tebou na výpravu.");
        }
        public void HatchCompanion()
        {
            var pet = CompanionRules.Hatch(Save.Companions, out bool duplicate); if (pet == null) return;
            RefreshCompanion(); Hud.ShowHatchResult(pet, duplicate);
        }
        public void BuyCompanionEgg()
        {
            if (!Save.Companions.StarterClaimed || Profile.Progress.Coins < CompanionRules.EggCost) return;
            Profile.Progress.Coins -= CompanionRules.EggCost; Save.Companions.Eggs++; ProfileStore.Save(Save); Hud.ShowPets();
        }
        public void SetCompanionGear(int gear)
        {
            var pet = Save.Companions.Equipped; if (pet == null) return;
            pet.Gear = Mathf.Clamp(gear, 0, pet.Level >= 5 ? 2 : pet.Level >= 3 ? 1 : 0); RefreshCompanion(); Hud.ShowPets();
        }
        private void RefreshCompanion()
        {
            offlineAvatar?.ApplyAppearance(Appearance());
            if (Connected && peers.TryGetValue(Session.LocalPlayer, out var local)) local.ApplyAppearance(Appearance());
            ProfileStore.Save(Save);
        }
        public void Claim(string id)
        {
            var r = Learning.ClaimQuest(Profile, id, DateTime.UtcNow);
            if (r.Claimed && Save.Companions.Equipped != null)
            {
                Save.Companions.Equipped.XP += r.PetXP;
                RefreshCompanion();
            }
            Notify(r.Claimed ? $"Odmena · +{r.XP} XP · +{r.Coins} mincí · +{r.PetXP} XP spoločníka" : r.Error);
            ProfileStore.Save(Save); Hud.ShowQuests();
        }
        public void Notify(string message) { Toast = message; toastUntil = Time.unscaledTime + 5; }
        public void TeleportOffline(Vector3 destination) { if (offlineController == null) return; offlineController.enabled = false; offline.transform.position = destination; offlineVelocity = Vector3.zero; offlineController.enabled = true; }
        public static Subject ParseSubject(string name) { switch (name) { case "math": return Subject.Mathematics; case "science": return Subject.Science; case "nature": return Subject.Nature; case "history": return Subject.History; case "language": return Subject.Languages; default: return Subject.Technology; } }
        public WorldLayout World => world;
        private void OnApplicationPause(bool paused) { if (!headless && paused && Save != null) ProfileStore.Save(Save); }
        private void OnApplicationQuit() { if (!headless && Save != null) ProfileStore.Save(Save); }
        private void OnDestroy() { NetworkExplorer.Spawned -= AddPeer; NetworkExplorer.Despawned -= RemovePeer; if (!headless && Save != null) ProfileStore.Save(Save); }
    }
}
