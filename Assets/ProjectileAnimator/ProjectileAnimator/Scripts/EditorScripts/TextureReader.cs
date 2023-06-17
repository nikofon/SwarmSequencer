using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using System;
using System.Linq;

namespace ProjectileAnimator
{
    public class TextureReader : EditorWindow
    {
        bool useAdditionalTextures;

        public List<Texture2D> TextureList = new List<Texture2D>();
        List<Texture2D> mainTextures;
        Color bgColor;
        Vector2 Center;
        List<FrameData> bakedData = new List<FrameData>();
        ListView TextureListView;
        ProgressBar bar;

        Dictionary<int, Texture2D> addTextures;

        IMGUIContainer cont;

        [MenuItem("Window/ProjectileAnimator/TextureReader (Legacy)")]
        public static void CreateWindow()
        {
            var window = GetWindow<TextureReader>();
            window.titleContent = new GUIContent("TextureReader");
        }

        private void OnEnable()
        {
            VisualTreeAsset origin = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ProjectileAnimator/ProjectileAnimator/UIDocuments/UXML/TextureReader.uxml");
            TemplateContainer container = origin.CloneTree();
            rootVisualElement.Add(container);
            rootVisualElement.Q<Button>("BakeButton").clicked += BakeTextureData;
            rootVisualElement.Q<TemplateContainer>().style.height = Length.Percent(100);
            bar = rootVisualElement.Q<ProgressBar>();
            CreateTextureList(rootVisualElement.Q("ListviewHolder"));

            cont = rootVisualElement.Q<IMGUIContainer>();

            cont.onGUIHandler += IMGUI;

            rootVisualElement.Q<Vector2Field>().RegisterValueChangedCallback((v) => Center = v.newValue);
            rootVisualElement.Q<Toggle>("UseAdditionalTextures").RegisterValueChangedCallback((v) => useAdditionalTextures = v.newValue);
        }
        private void OnDisable()
        {
            rootVisualElement.Q<Button>("BakeButton").clicked -= BakeTextureData;
        }

        void IMGUI()
        {
            if (TextureListView.selectedIndices != null)
            {
                float width = cont.localBound.width;
                float height = Mathf.Clamp(cont.localBound.height / TextureListView.selectedIndices.Count(), 0, width);
                int k = 0;
                var sortable = TextureListView.selectedIndices.ToList();
                sortable.Sort();
                foreach (int i in sortable)
                {
                    EditorGUI.DrawPreviewTexture(new Rect(k * height * Vector2.up, new Vector2(width, height)), TextureList[i]);
                    k++;
                }
            }
        }
        void CreateTextureList(VisualElement parent)
        {
            Func<VisualElement> makeItem = () => new Label();

            Action<VisualElement, int> bindItem = (e, i) => { if (i >= 0 && i < TextureList.Count) (e as Label).text = TextureList[i].name; };

            const int itemHeight = 16;

            var listView = new ListView(TextureList, itemHeight, makeItem, bindItem);

            listView.selectionType = SelectionType.Multiple;

            listView.style.height = Length.Percent(100);

            listView.style.flexGrow = 1.0f;

            listView.reorderable = true;

            listView.RegisterCallback<KeyDownEvent>((x) => { if (x.keyCode == KeyCode.Delete) { DeleteListEnterance(listView); } });
            listView.RegisterCallback<DragEnterEvent>(x => DragEntered());
            listView.RegisterCallback<DragLeaveEvent>(x => DragExited());
            listView.RegisterCallback<DragUpdatedEvent>(x => { });
            listView.RegisterCallback<DragPerformEvent>(x => { });
            listView.RegisterCallback<DragExitedEvent>(x => DragPerformed());

            TextureListView = listView;

            parent.Add(listView);
        }

        void DeleteListEnterance(ListView deleteFrom)
        {
            if (deleteFrom.selectedIndices != null)
            {
                var toDelete = deleteFrom.selectedIndices.ToList();
                toDelete.Sort();
                for (int i = toDelete.Count - 1; i >= 0; i--)
                {
                    TextureList.RemoveAt(toDelete[i]);
                }
                deleteFrom.Rebuild();
            }
        }

        void BakeTextureData()
        {
            bakedData.Clear();
            int i = 0;
            mainTextures = new List<Texture2D>(TextureList);
            if (useAdditionalTextures)
            {
                SeparateAdditionalTextures();
            }
            foreach (Texture2D t in mainTextures)
            {
                Color[] colors = t.GetPixels();
                var res = BakeTexture(t.width, t.height, Center.y, Center.x, i, ref colors);
                i++;
                if (res.ProjectilePositionData.Count != 0)
                {
                    bakedData.Add(res);
                }
                bar.value = i / (float)mainTextures.Count;
            }
            SerializeResult();
        }

        void DragEntered()
        {
            bgColor = EditorGUIUtility.isProSkin ? new Color32(56, 56, 56, 255) : new Color32(194, 194, 194, 255);
            TextureListView.style.backgroundColor = new Color(1, 1, 1, 0.1f);
        }

        void DragExited()
        {
            TextureListView.style.backgroundColor = bgColor;
        }

        void DragPerformed()
        {
            TextureListView.style.backgroundColor = bgColor;
            foreach (var v in DragAndDrop.objectReferences)
            {
                if (v.GetType() == typeof(Texture2D))
                {
                    TextureList.Add(v as Texture2D);
                }
            }

            TextureListView.Rebuild();
        }

        void SeparateAdditionalTextures()
        {
            Debug.Log("SeparatingTextures");
            string toSearch = "AdditionalTexture";
            addTextures = new Dictionary<int, Texture2D>();
            foreach (var t in TextureList)
            {
                if (t.name.Contains(toSearch))
                {
                    int index = 0;
                    mainTextures.Remove(t);
                    string res = t.name.Substring(t.name.LastIndexOf("AdditionalTexture") + toSearch.Length);
                    if (int.TryParse(res, out index)) { addTextures.Add(index, t); }
                    else throw new NamingViolationException($"Can't parse {res} to int. Rename texture {t.name} following rules!");
                }
            }
            Debug.Log($"{addTextures.Count} textures sepatated");
        }

        bool SerializeResult()
        {
            string path = EditorUtility.SaveFilePanel("SaveProjectileBakedData", Application.dataPath, "ProjectileBakedData", "pbd.txt");
            FrameDataSerializer.SaveFrameData(path, bakedData);
            AssetDatabase.Refresh();
            return true;
        }

        FrameData BakeTexture(int width, int height, float heightMid, float widthMid, int index, ref Color[] colors)
        {
            var res = new Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>>();
            int n = 0;
            foreach (Color c in colors)
            {
                if (c.r != 0)
                {
                    if (useAdditionalTextures)
                    {
                        if (Mathf.RoundToInt(c.a * 255) != 255)
                        {
                            Debug.Log($"calling bakedata from additional texture for color {c}, pos {n % width} {n / height}");
                            BakeDataFromAdditionalTexture(Mathf.RoundToInt(c.a * 255), n % width, n / height, heightMid, widthMid, ref res);
                        }
                    }
                    var key = new ProjectileKey(Mathf.RoundToInt(c.r * 255), Mathf.RoundToInt(c.g * 255));
                    if (res.ContainsKey(key))
                        throw new InstanceConflictException($"Texture already contains pixel with values {Mathf.RoundToInt(c.r * 255)} {Mathf.RoundToInt(c.g * 255)} at {n % width} {n / height}. Previous pixel with the same key is at {res[key].Item1.x + widthMid} {res[key].Item1.y + heightMid}");
                    res.Add(key, new Tuple<SerializableVector3, SerializableVector3>(new SerializableVector3(n % width - widthMid, n / height - heightMid, c.b * 255 - 127), SerializableVector3.Infinity));
                }
                n++;
            }

            return new FrameData(res, index);
        }

        void BakeDataFromAdditionalTexture(int textureNumber, int width, int height, float heightMid, float widthMid, ref Dictionary<ProjectileKey, Tuple<SerializableVector3, SerializableVector3>> res)
        {
            if (!addTextures.ContainsKey(textureNumber)) { throw new KeyNotFoundException($"Can't find additional texture with id {textureNumber}"); }
            var t = addTextures[textureNumber];
            Color c = t.GetPixel(width, height);
            var key = new ProjectileKey(Mathf.RoundToInt(c.r * 255), Mathf.RoundToInt(c.g * 255));
            if (res.ContainsKey(key))
                throw new InstanceConflictException($"Texture already contains pixel with values {Mathf.RoundToInt(c.r * 255)} {Mathf.RoundToInt(c.g * 255)} at {width} {height}. Previous pixel with the same key is at {res[key].Item1.x + widthMid} {res[key].Item1.y + heightMid}");
            res.Add(key, new Tuple<SerializableVector3, SerializableVector3>(new SerializableVector3(width - widthMid, height - heightMid, c.b * 255 - 127), SerializableVector3.Infinity));
            if (Mathf.RoundToInt(c.a * 255) != 255)
            {
                if (textureNumber == c.a) { throw new SelfReferencingLoopException($"Texture {textureNumber} is pointing to itself at {width} & {height}"); }
                else
                {
                    Debug.Log($"Calling bake data from additional texture for color {c}");
                    BakeDataFromAdditionalTexture(Mathf.RoundToInt(c.a * 255), width, height, heightMid, widthMid, ref res);
                }
            }
        }

    }
}

