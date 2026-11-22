using System;
using System.Collections.Generic;
using Lumen.Core;
using Lumen.World;
using UnityEngine;

namespace Lumen.Runtime
{
    public sealed class ExpeditionDestination
    {
        public readonly int Index;
        public readonly string Id, Name, Description, Objective;
        public readonly Subject Subject;
        public readonly Vector3 Anchor;
        public readonly Color Accent;
        internal ExpeditionDestination(int index, string description, string objective, Subject subject, Color accent)
        {
            Index = index; Id = AcademyWorld.ExpeditionIds[index]; Name = AcademyWorld.ExpeditionNames[index];
            Anchor = AcademyWorld.ExpeditionAnchors[index]; Description = description; Objective = objective;
            Subject = subject; Accent = accent;
        }
    }

    public sealed partial class LumenGame
    {
        private static readonly IReadOnlyList<ExpeditionDestination> expeditionDestinations = Array.AsReadOnly(new[] {
            new ExpeditionDestination(0, "Prílivový bazén, meracie stĺpiky a archív rovnakých častí.", "Preskúmaj model zlomkov, otvor učebnicu a over si tému v teste so 6 otázkami.", Subject.Mathematics, new Color(.56f,.65f,.64f)),
            new ExpeditionDestination(1, "Zamerané kamenné ruiny s trojuholníkom 3 : 4 : 5 a geodetickou sieťou.", "Prepoj dĺžky a tvary s postupom v učebnici; zvládni geometrický tematický test.", Subject.History, new Color(.72f,.61f,.43f)),
            new ExpeditionDestination(2, "Staré observatórium, rovnovážny prístroj a archív neznámych.", "Vysvetli rovnováhu rovnice a over si algebraické postupy v tematickom teste.", Subject.Nature, new Color(.56f,.60f,.49f))
        });
        public IReadOnlyList<ExpeditionDestination> Expeditions => expeditionDestinations;
        public int ActiveExpeditionIndex
        {
            get
            {
                if (headless || offline == null) return -1;
                Vector3 position = PlayerPosition;
                if (position.y < -4 || position.y > 18) return -1;
                for (int i = 0; i < AcademyWorld.ExpeditionCenters.Length; i++)
                {
                    Vector3 delta = position - AcademyWorld.ExpeditionCenters[i]; delta.y = 0;
                    if (delta.sqrMagnitude <= 12.5f * 12.5f) return i;
                }
                return -1;
            }
        }

        public void StartExpedition(int index)
        {
            if (index < 0 || index >= Expeditions.Count || headless) return;
            if (!CanTravelByExpeditionPortal()) return;
            StopCourseForExpedition();
            Hud.CloseModal();
            FaceExpeditionArrival(index == 2 ? 180 : 0);
            if (SharedConnected) RequestSharedRespawn("expedition", index, Expeditions[index].Anchor);
            else TeleportOffline(Expeditions[index].Anchor);
            if (ViewCamera != null) UpdateCamera(1);
            Notify(Expeditions[index].Name + " · učebnica vľavo, tematický test vpravo. Návratový portál je pri mieste príchodu.");
        }

        public void ReturnFromExpedition()
        {
            if (headless || !CanTravelByExpeditionPortal()) return;
            StopCourseForExpedition(); Hud.CloseModal();
            FaceExpeditionArrival(180);
            if (SharedConnected) RequestSharedRespawn("expedition", 3, AcademyWorld.ExpeditionAnchors[3]);
            else TeleportOffline(AcademyWorld.ExpeditionAnchors[3]);
            if (ViewCamera != null) UpdateCamera(1);
            Notify("Návrat do akadémie Numerion. Rozpracovaný test a získaný pokrok zostávajú uložené.");
        }

        private bool CanTravelByExpeditionPortal()
        {
            // Legacy UDP movement remains server-authoritative; it has no expedition respawn RPC.
            if (Connected && !SharedConnected) { Notify("Portály fungujú v sólo svete a v spoločnej online miestnosti. Pred výpravou odíď z LAN relácie cez menu hrania."); return false; }
            if (SharedConnected && Online.Busy) { Notify("Počkaj na dokončenie požiadavky spoločnej miestnosti a otvor portál znova."); return false; }
            return offlineController != null;
        }

        private void StopCourseForExpedition()
        {
            CourseActive = false; courseFall = 0; jumpQueued = false;
        }

        private void FaceExpeditionArrival(float heading)
        {
            yaw = heading;
            if (offline != null) offline.transform.rotation = Quaternion.Euler(0, heading, 0);
        }

        private void InteractWithExpeditionStation()
        {
            string id = Target?.Id ?? "";
            if (id == "expedition-hub") { Hud.ShowExpeditions(); return; }
            if (id.StartsWith("expedition-return-", StringComparison.Ordinal)) { ReturnFromExpedition(); return; }
            int separator = id.LastIndexOf('-');
            if (separator < 0 || !int.TryParse(id.Substring(separator + 1), out int index) || index < 0 || index >= Expeditions.Count)
            { Hud.ShowExpeditions(); return; }
            if (id.StartsWith("expedition-portal-", StringComparison.Ordinal)) { StartExpedition(index); return; }
            if (id.StartsWith("expedition-test-", StringComparison.Ordinal)) { BeginStudyTest(Expeditions[index].Subject); return; }
            if (id.StartsWith("expedition-lesson-", StringComparison.Ordinal))
            {
                var lessons = LessonCatalog.GetLessons(Profile.Grade, Expeditions[index].Subject);
                if (lessons.Count > 0) Hud.ShowLesson(lessons[0]); else Hud.ShowStudy();
                return;
            }
            Hud.ShowExpeditions();
        }
    }
}
