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
        public List<Texture2D> TextureList = new List<Texture2D>();
        Color bgColor;
        Vector2 Center;
        List<FrameData> bakedData = new List<FrameData>();
        ListView TextureListView;
        ProgressBar bar;

        IMGUIContainer cont;

        [MenuItem("Window/ProjectileAnimator/TextureReader")]
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
                float height = Mathf.Clamp(cont.localBound.height/ TextureListView.selectedIndices.Count(), 0, width);
                int k = 0;
                var sortable = TextureListView.selectedIndices.ToList();
                sortable.Sort();
                foreach (int i in sortable)
                {
                    EditorGUI.DrawPreviewTexture(new Rect( k*height * Vector2.up, new Vector2(width, height)), TextureList[i]);
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
                for(int i = toDelete.Count - 1; i >= 0; i--)
                {
                    TextureList.RemoveAt(toDelete[i]);
                }
                deleteFrom.Refresh();
            }
        }

        void BakeTextureData() {
            bakedData.Clear();
            int i = 0;
            foreach(Texture2D t in TextureList)
            {
                Color[] colors = t.GetPixels();
                var res = BakeTexture(t.width, t.height, Center.y, Center.x, i, ref colors);
                i++;
                if(res.ProjectilePositionData.Count != 0)
                {
                    bakedData.Add(res);
                }
                bar.value = i /(float) TextureList.Count;
            }
            SerializeResult();
        }

        void DragEntered()
        {
            bgColor = EditorGUIUtility.isProSkin? new Color32(56, 56, 56, 255): new Color32(194, 194, 194, 255);
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
                if(v.GetType() == typeof(Texture2D))
                {
                    TextureList.Add(v as Texture2D);
                }
            }
            
            TextureListView.Refresh();
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
            var res = new Dictionary<ProjectileKey, SerializableVector3>();
            int n = 0;
            foreach (Color c in colors)
            {
                if (c.a != 0)
                {
                    var key = new ProjectileKey(Mathf.RoundToInt(c.r * 255), Mathf.RoundToInt(c.g * 255));
                    if (res.ContainsKey(key))
                        throw new InstanceConflictException($"Texture already contains pixel with values {Mathf.RoundToInt(c.r * 255)} {Mathf.RoundToInt(c.g * 255)} at {n % width} {n / height}. Previous pixel with the same key is at {res[key].x + widthMid} {res[key].y + heightMid}");
                    res.Add(key, new SerializableVector3(n % width - widthMid, n / height - heightMid, c.b * 255 - 127));
                }
                n++;
            }

            return new FrameData(res, index);
        }

    }
}

