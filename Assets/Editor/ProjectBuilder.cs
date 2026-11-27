using System.Collections.Generic;
using System.IO;
using SlotGame.Data;
using SlotGame.Machine;
using SlotGame.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SlotGame.EditorTools
{
    /// <summary>
    /// Rebuilds every generated asset (symbol/config ScriptableObjects, the symbol-tile
    /// prefab, the win-pulse clip and the whole main scene) from the imported art.
    ///
    /// Keeping the scene under script means the layout is reviewable in source control
    /// and reproducible: change a constant here, re-run, and the scene is rebuilt the
    /// same way every time instead of being hand-nudged in the editor.
    /// </summary>
    public static class ProjectBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string CanvasSize = "1024x768";

        // Machine art is 816x624. These are the measured reel-window rects inside it.
        private const float WindowWidth = 108f;
        private const float WindowHeight = 204f;
        private const float WindowCentreY = -37f;
        private const float RowHeight = WindowHeight / 3f;
        private static readonly float[] WindowCentreX = { -125.5f, 4.5f, 134.5f };

        private static Font _font;

        private static Font GameFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null)
                    {
                        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    }
                }

                return _font;
            }
        }

        [MenuItem("Slot Game/1. Build Project (assets + scene)", priority = 1)]
        public static void BuildAll()
        {
            EnsureFolders();

            SymbolDefinition[] symbols = CreateSymbols();
            GameConfig config = CreateConfig(symbols);
            SymbolTileView tilePrefab = CreateTilePrefab();
            RuntimeAnimatorController pulse = CreateWinPulseController();

            CreateScene(config, tilePrefab, pulse);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ProjectBuilder] Project built: assets, prefab, animator and scene.");
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[]
                     {
                         "Assets/Scenes", "Assets/Prefabs", "Assets/Animations", "Assets/Settings",
                         "Assets/Settings/Symbols"
                     })
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }

            AssetDatabase.Refresh();
        }

        // ------------------------------------------------------------------ data assets

        private static SymbolDefinition[] CreateSymbols()
        {
            // name, sprite file, weight, payout multiplier (x line bet)
            var definitions = new[]
            {
                ("Cherry", "slot-symbol2", 40, 5),
                ("Bar", "slot-symbol4", 30, 10),
                ("Bell", "slot-symbol3", 20, 35),
                ("Seven", "slot-symbol1", 10, 50)
            };

            var created = new SymbolDefinition[definitions.Length];
            for (int i = 0; i < definitions.Length; i++)
            {
                (string name, string file, int weight, int payout) = definitions[i];
                string path = $"Assets/Settings/Symbols/{name}.asset";
                AssetDatabase.DeleteAsset(path);

                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Symbols/{file}.png");
                if (icon == null)
                {
                    Debug.LogError($"[ProjectBuilder] Missing symbol sprite Assets/Art/Symbols/{file}.png");
                }

                var symbol = ScriptableObject.CreateInstance<SymbolDefinition>();
                symbol.Configure(name.ToLowerInvariant(), name, icon, weight, payout);
                AssetDatabase.CreateAsset(symbol, path);
                created[i] = symbol;
            }

            return created;
        }

        private static GameConfig CreateConfig(SymbolDefinition[] symbols)
        {
            const string path = "Assets/Settings/GameConfig.asset";
            AssetDatabase.DeleteAsset(path);

            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.Configure(symbols);
            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        // ---------------------------------------------------------------------- prefabs

        private static SymbolTileView CreateTilePrefab()
        {
            const string path = "Assets/Prefabs/SymbolTile.prefab";

            var go = new GameObject("SymbolTile", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(RowHeight, RowHeight);

            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;

            var view = go.AddComponent<SymbolTileView>();
            view.Configure(image);
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<SymbolTileView>();
        }

        private static RuntimeAnimatorController CreateWinPulseController()
        {
            const string clipPath = "Assets/Animations/WinPulse.anim";
            const string controllerPath = "Assets/Animations/WinPulse.controller";

            AssetDatabase.DeleteAsset(controllerPath);
            AssetDatabase.DeleteAsset(clipPath);

            var clip = new AnimationClip { name = "WinPulse", frameRate = 30f, wrapMode = WrapMode.Loop };
            var curve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.45f, 1.09f),
                new Keyframe(1f, 1f));

            foreach (string axis in new[] { "x", "y" })
            {
                AnimationUtility.SetEditorCurve(
                    clip,
                    EditorCurveBinding.FloatCurve(string.Empty, typeof(Transform), $"m_LocalScale.{axis}"),
                    curve);
            }

            AssetDatabase.CreateAsset(clip, clipPath);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddMotion(clip);
            AssetDatabase.SaveAssets();

            return controller;
        }

        // ------------------------------------------------------------------------ scene

        private static void CreateScene(GameConfig config, SymbolTileView tilePrefab, RuntimeAnimatorController pulse)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            var camera = Object.FindObjectOfType<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.03f, 0.10f);
            camera.orthographic = true;
            camera.tag = "MainCamera";

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1024f, 768f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            // --- background -------------------------------------------------------
            Sprite background = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/bg_gradient.png");
            Image backgroundImage = CreateImage("Background", canvasGo.transform, background, Vector2.zero, Vector2.zero);
            Stretch(backgroundImage.rectTransform);

            // --- machine ----------------------------------------------------------
            Sprite machineSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Machine/slot-machine1.png");
            Vector2 machineSize = machineSprite != null ? machineSprite.rect.size : new Vector2(816f, 624f);
            Image machine = CreateImage("Machine", canvasGo.transform, machineSprite, Vector2.zero, machineSize);

            // Payline highlights are created after the reels so the glow sits on top of
            // the symbols; a low alpha keeps the art readable underneath.
            var highlights = new GameObject[WindowCentreX.Length];
            for (int row = 0; row < highlights.Length; row++)
            {
                float y = WindowCentreY + (1 - row) * RowHeight;
                RectTransform bar = CreateRect($"WinHighlight_Row{row}", machine.transform, new Vector2(4.5f, y), new Vector2(388f, RowHeight));
                var image = bar.gameObject.AddComponent<Image>();
                image.color = new Color(1f, 0.82f, 0.20f, 0.30f);
                image.raycastTarget = false;

                var animator = bar.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = pulse;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                bar.gameObject.SetActive(false);
                highlights[row] = bar.gameObject;
            }

            // --- reels ------------------------------------------------------------
            var reels = new Reel[WindowCentreX.Length];
            for (int i = 0; i < reels.Length; i++)
            {
                reels[i] = CreateReel(machine.transform, i, tilePrefab, config.FillerSymbolsPerReel);
            }

            // keep the highlights above the reels in draw order
            foreach (GameObject highlight in highlights)
            {
                highlight.transform.SetAsLastSibling();
            }

            // --- HUD: credits (top-left) -----------------------------------------
            RectTransform creditRoot = CreateRect("CreditsPanel", canvasGo.transform, Vector2.zero, new Vector2(280f, 120f));
            Anchor(creditRoot, new Vector2(0f, 1f), new Vector2(24f, -24f), new Vector2(0f, 1f));
            Text creditsText = CreateText("CreditsText", creditRoot, Vector2.zero, new Vector2(280f, 120f), 34, TextAnchor.UpperLeft);
            Stretch(creditsText.rectTransform);

            // --- message strip under the reels ------------------------------------
            Sprite middleBox = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Machine/slot_machine_Middle_box.png");
            CreateImage("MessageBox", machine.transform, middleBox, new Vector2(0f, -252f), new Vector2(560f, 116f));

            Text betText = CreateText("BetText", machine.transform, new Vector2(-140f, -252f), new Vector2(210f, 56f), 30, TextAnchor.MiddleCenter);
            Text winText = CreateText("WinText", machine.transform, new Vector2(140f, -252f), new Vector2(210f, 56f), 30, TextAnchor.MiddleCenter);

            // --- spin button (art triangle, left of the cabinet) -------------------
            Sprite[] spinFrames = LoadFrames("Assets/Art/Buttons/slot_machine_buttons-03.png",
                "frame_0", "frame_1", "frame_2", "frame_3");
            SpriteStateButton spinButton = CreateSpriteButton("SpinButton", canvasGo.transform, new Vector2(-386f, -40f), new Vector2(152f, 152f), spinFrames);

            // --- bet panel (right) -------------------------------------------------
            Sprite panelSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Image panel = CreateImage("BetPanel", canvasGo.transform, panelSprite, new Vector2(415f, 30f), new Vector2(170f, 330f));
            panel.type = Image.Type.Sliced;
            panel.color = new Color(0.12f, 0.11f, 0.26f, 0.95f);
            panel.raycastTarget = true;

            Text betHeader = CreateText("BetHeader", panel.transform, new Vector2(0f, 128f), new Vector2(150f, 40f), 22, TextAnchor.MiddleCenter);
            betHeader.text = "SELECT BET";
            betHeader.color = new Color(0.85f, 0.88f, 1f);

            var betButtons = new Button[3];
            for (int i = 0; i < betButtons.Length; i++)
            {
                betButtons[i] = CreateLabelButton(
                    $"BetButton_{i}",
                    panel.transform,
                    new Vector2(0f, 70f - i * 62f),
                    new Vector2(146f, 52f),
                    $"BET {config.BetLevels[i]}G");
            }

            Sprite[] exitFrames = LoadFrames("Assets/Art/Buttons/slot_machine_buttons-02.png",
                "frame_0", "frame_1", "frame_2", "frame_3");
            SpriteStateButton exitButton = CreateSpriteButton("ExitButton", panel.transform, new Vector2(0f, -110f), new Vector2(72f, 72f), exitFrames);
            var quitter = exitButton.gameObject.AddComponent<QuitButton>();
            quitter.Configure(exitButton);

            // --- paytable (left of the cabinet) -----------------------------------
            Text paytable = CreateText("Paytable", canvasGo.transform, new Vector2(-392f, -228f), new Vector2(196f, 170f), 18, TextAnchor.UpperLeft);
            paytable.color = new Color(0.86f, 0.89f, 1f, 0.92f);
            paytable.text = "MATCH 3 ON A LINE\nSEVEN  50x\nBELL   35x\nBAR    10x\nCHERRY  5x\n\nx LINE BET";

            // --- status line -------------------------------------------------------
            Text statusText = CreateText("StatusText", canvasGo.transform, new Vector2(0f, -342f), new Vector2(700f, 44f), 24, TextAnchor.MiddleCenter);
            statusText.color = new Color(0.82f, 0.86f, 1f);
            statusText.text = "PRESS SPIN";

            // --- gamble popup ------------------------------------------------------
            (GameObject popupRoot, Text title, Text amount, Button yes, Button no) = CreateGamblePopup(canvasGo.transform);

            // --- machine component + view -----------------------------------------
            var machineGo = new GameObject("SlotMachine", typeof(RectTransform));
            machineGo.GetComponent<RectTransform>().SetParent(canvasGo.transform, false);
            var slotMachine = machineGo.AddComponent<SlotMachine>();
            slotMachine.Configure(config, reels);

            var smokeTest = machineGo.AddComponent<SlotGame.Diagnostics.SmokeTestRunner>();
            smokeTest.Configure(slotMachine);

            var captureRunner = machineGo.AddComponent<SlotGame.Diagnostics.CaptureRunner>();
            captureRunner.Configure(slotMachine);

            var view = machineGo.AddComponent<SlotMachineView>();
            view.Configure(
                slotMachine,
                creditsText,
                betText,
                winText,
                statusText,
                spinButton,
                betButtons,
                highlights,
                popupRoot,
                title,
                amount,
                yes,
                no);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[ProjectBuilder] Scene saved to {ScenePath} ({CanvasSize} reference resolution).");
        }

        private static Reel CreateReel(Transform machine, int index, SymbolTileView tilePrefab, int filler)
        {
            RectTransform window = CreateRect(
                $"Reel_{index}",
                machine,
                new Vector2(WindowCentreX[index], WindowCentreY),
                new Vector2(WindowWidth, WindowHeight));
            window.gameObject.AddComponent<RectMask2D>();

            RectTransform strip = CreateRect("Strip", window, Vector2.zero, new Vector2(WindowWidth, 0f));
            strip.anchorMin = new Vector2(0.5f, 1f);
            strip.anchorMax = new Vector2(0.5f, 1f);
            strip.pivot = new Vector2(0.5f, 1f);
            strip.anchoredPosition = Vector2.zero;

            var reel = window.gameObject.AddComponent<Reel>();
            reel.Configure(strip, tilePrefab, RowHeight, filler);
            return reel;
        }

        private static (GameObject root, Text title, Text amount, Button yes, Button no) CreateGamblePopup(Transform parent)
        {
            RectTransform root = CreateRect("GamblePopup", parent, Vector2.zero, Vector2.zero);
            Stretch(root);

            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.66f);
            dim.raycastTarget = true;

            Sprite popupSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/popup.png");
            CreateImage("PopupPanel", root, popupSprite, new Vector2(0f, 20f), new Vector2(640f, 323f));

            Text title = CreateText("PopupTitle", root, new Vector2(0f, 115f), new Vector2(520f, 50f), 34, TextAnchor.MiddleCenter);
            title.text = "YOU WIN";

            Text amount = CreateText("PopupAmount", root, new Vector2(0f, 45f), new Vector2(520f, 72f), 54, TextAnchor.MiddleCenter);
            amount.text = "0 G";
            amount.color = new Color(1f, 0.86f, 0.35f);

            Text hint = CreateText("PopupHint", root, new Vector2(0f, -8f), new Vector2(520f, 40f), 22, TextAnchor.MiddleCenter);
            hint.text = "DOUBLE IT OR TAKE IT?";
            hint.color = new Color(0.80f, 0.84f, 1f);

            Sprite[] yesFrames = LoadFrames("Assets/Art/Buttons/Yes_No_Btn.png",
                "yes_normal", "yes_hover", "yes_pressed", "yes_pressed");
            Sprite[] noFrames = LoadFrames("Assets/Art/Buttons/Yes_No_Btn.png",
                "no_normal", "no_hover", "no_pressed", "no_pressed");

            SpriteStateButton yes = CreateSpriteButton("GambleYes", root, new Vector2(-120f, -85f), new Vector2(180f, 84f), yesFrames);
            SpriteStateButton no = CreateSpriteButton("GambleNo", root, new Vector2(120f, -85f), new Vector2(180f, 84f), noFrames);

            root.gameObject.SetActive(false);
            return (root.gameObject, title, amount, yes, no);
        }

        // ---------------------------------------------------------------------- helpers

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Sprite sprite, Vector2 position, Vector2 size)
        {
            RectTransform rect = CreateRect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        private static Text CreateText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, TextAnchor anchor)
        {
            RectTransform rect = CreateRect(name, parent, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = GameFont;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            return text;
        }

        private static SpriteStateButton CreateSpriteButton(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Sprite[] frames)
        {
            RectTransform rect = CreateRect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = frames.Length > 0 ? frames[0] : null;
            image.preserveAspect = true;

            var button = rect.gameObject.AddComponent<SpriteStateButton>();
            button.targetGraphic = image;
            button.Configure(
                Frame(frames, 0),
                Frame(frames, 1),
                Frame(frames, 2),
                Frame(frames, 3));
            return button;
        }

        private static Button CreateLabelButton(string name, Transform parent, Vector2 position, Vector2 size, string label)
        {
            RectTransform rect = CreateRect(name, parent, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            image.type = Image.Type.Sliced;
            image.color = new Color(0.22f, 0.24f, 0.40f, 1f);

            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText("Label", rect, Vector2.zero, size, 24, TextAnchor.MiddleCenter);
            text.text = label;
            Stretch(text.rectTransform);
            return button;
        }

        private static Sprite Frame(Sprite[] frames, int index)
        {
            if (frames == null || frames.Length == 0)
            {
                return null;
            }

            return frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        }

        private static Sprite[] LoadFrames(string path, params string[] names)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            var frames = new Sprite[names.Length];

            for (int i = 0; i < names.Length; i++)
            {
                foreach (Object asset in assets)
                {
                    if (asset is Sprite sprite && sprite.name == names[i])
                    {
                        frames[i] = sprite;
                        break;
                    }
                }

                if (frames[i] == null)
                {
                    Debug.LogWarning($"[ProjectBuilder] Sprite '{names[i]}' not found in {path}");
                }
            }

            return frames;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 position, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = position;
        }
    }
}
