using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lumen.Runtime
{
    public sealed partial class LumenHud
    {
        public void ApplyInterfaceScale(float value)
        { ConfigureInterfaceScale(value); }

        public void ShowSettings()
        {
            Open("Nastavenia hry", "LUMEN / TVOJ SPÔSOB HRANIA"); FeaturePanel = "SETTINGS";
            var options = game.Save.Options;
            Label(body, "ZVUK A HUDBA", 26, Purple);
            OptionSlider("Celková hlasitosť", options.MasterVolume, 0, 1, v => options.MasterVolume = v, v => Mathf.RoundToInt(v * 100) + " %");
            OptionSlider("Hudba akadémie", options.MusicVolume, 0, 1, v => options.MusicVolume = v, v => Mathf.RoundToInt(v * 100) + " %");
            OptionSlider("Efekty a spätná väzba", options.EffectsVolume, 0, 1, v => options.EffectsVolume = v, v => Mathf.RoundToInt(v * 100) + " %");
            ButtonNode(body, "Vyskúšať zvuk odmeny", () => game.Audio?.Answer(true), Mint, Ink, 24);
            Label(body, "KAMERA A ČITATEĽNOSŤ", 26, Purple);
            OptionSlider("Citlivosť myši", options.MouseSensitivity, .4f, 2.5f, v => options.MouseSensitivity = v, v => v.ToString("0.0") + "×");
            OptionSlider("Zorné pole kamery", options.FieldOfView, 50, 85, v => options.FieldOfView = v, v => Mathf.RoundToInt(v) + "°");
            OptionSlider("Veľkosť rozhrania", options.InterfaceScale, .85f, 1.2f, v => options.InterfaceScale = v, v => Mathf.RoundToInt(v * 100) + " %");
            var invert = CheckBox(body, "Obrátiť zvislý pohyb kamery", options.InvertCamera); invert.onValueChanged.AddListener(v => { options.InvertCamera = v; game.ApplyUserOptions(); });
            var motion = CheckBox(body, "Obmedziť pohyblivé dekorácie", options.ReduceMotion); motion.onValueChanged.AddListener(v => { options.ReduceMotion = v; game.ApplyUserOptions(); });
            var names = CheckBox(body, "Zobrazovať mená spoluhráčov", options.ShowNames); names.onValueChanged.AddListener(v => { options.ShowNames = v; game.ApplyUserOptions(); });
            var helper = CheckBox(body, "Zobrazovať pomoc peta pri úlohách", options.PetHelp); helper.onValueChanged.AddListener(v => { options.PetHelp = v; game.ApplyUserOptions(); });
            Label(body, "GRAFIKA, OVLÁDANIE A PROFIL", 26, Purple);
            ButtonNode(body, "Grafika a výkon · kvalita, 4K, FPS", ShowGraphics, Lavender);
            ButtonNode(body, "Prepnúť celú obrazovku / okno", () => Screen.fullScreen = !Screen.fullScreen, Cream);
            ButtonNode(body, "Ovládanie a sprievodca", ShowTutorial, Cream);
            ButtonNode(body, "Postava a ročník", () => ShowProfile(false), Cream);
            ButtonNode(body, "Ukladanie a prenos postupu", ShowCloud, Cream);
            Label(body, "WASD / šípky: pohyb · Shift: beh · medzerník: skok · pravé tlačidlo myši: kamera · E: interakcia · M: mapa · J: denník · Esc: menu. Vo VR určuje zorné pole headset.", 23, Muted);
            ButtonNode(body, "Obnoviť tieto nastavenia…", () => { Open("Obnoviť nastavenia?", "NASTAVENIA"); Label(body, "Zvuk, kamera a rozhranie sa vrátia na predvolené hodnoty. Tvoj herný postup zostáva uložený.", 26, Ink); ButtonNode(body, "Obnoviť nastavenia", () => { game.Save.Options = new GameOptions(); game.ApplyUserOptions(); ProfileStore.Save(game.Save); ShowSettings(); }, Lavender); ButtonNode(body, "Späť", ShowSettings, Lime); }, Cream, Muted, 23);
            ButtonNode(body, "Uložiť a pokračovať", () => { ProfileStore.Save(game.Save); CloseModal(); }, Lime);
        }
        private void OptionSlider(string name, float value, float min, float max, Action<float> changed, Func<float,string> format)
        {
            var card = Card(body, Cream); var label = Label(card, name + " · " + format(value), 26, Ink);
            var track = ImageNode("Slider / " + name, card, Lavender); SetHeight(track.rectTransform, 46);
            var slider = track.gameObject.AddComponent<Slider>(); slider.minValue = min; slider.maxValue = max;
            var fillArea = new GameObject("Fill area", typeof(RectTransform)).GetComponent<RectTransform>(); fillArea.SetParent(track.transform, false); Stretch(fillArea, new Vector2(0,.25f), new Vector2(1,.75f)); fillArea.offsetMin = new Vector2(18,0); fillArea.offsetMax = new Vector2(-18,0);
            var fill = ImageNode("Fill", fillArea, Purple); Stretch(fill.rectTransform, Vector2.zero, Vector2.one); slider.fillRect = fill.rectTransform;
            var handleArea = new GameObject("Handle area", typeof(RectTransform)).GetComponent<RectTransform>(); handleArea.SetParent(track.transform, false); Stretch(handleArea, Vector2.zero, Vector2.one); handleArea.offsetMin = new Vector2(18,0); handleArea.offsetMax = new Vector2(-18,0);
            var handle = ImageNode("Handle", handleArea, Ink); handle.rectTransform.sizeDelta = new Vector2(30,44); slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
            slider.SetValueWithoutNotify(value); slider.onValueChanged.AddListener(v => { changed(v); label.text = name + " · " + format(v); game.ApplyUserOptions(); });
        }
    }
}
