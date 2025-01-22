using UnityEngine;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using System;
using System.Collections;
using System.Linq;
using UnityEditor.Playables;

public class ImageExporterWindow : EditorWindow
{
    public Texture2D bigImage;  // 导入的大图
    public int spacing = 2;  // 小图之间的间隔像素
    public string outputDirectory = "Assets/Output";  // 导出目录
    private static ImageExporterWindow Instance;

    [MenuItem("Window/ZyTool/ExportImages")]
    static public void ExportImages0() 
    {
       Instance= EditorWindow.GetWindow<ImageExporterWindow>(typeof(ImageExporterWindow));
    }
  
     List<Vector2Int> list=new List<Vector2Int>();
     List<RectInt> listRect=new List<RectInt>();
    
     Rect rUi;
     Texture2D mask;
     bool isShowMask=true;
     bool isCancel=false;
     Vector2 offsetScrollview=new Vector2(0,0);
     private float viewSize=1;
    private void OnGUI() 
    {

        EditorGUI.BeginChangeCheck();
        bigImage = EditorGUILayout.ObjectField(bigImage, typeof(Texture2D), true) as Texture2D;
        if (EditorGUI.EndChangeCheck())
        {
            visited = null;
            if (bigImage)
            {
                string path = AssetDatabase.GetAssetPath(bigImage);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                textureImporter.alphaIsTransparency = true;
                textureImporter.mipmapEnabled = false;
                textureImporter.npotScale = TextureImporterNPOTScale.None;
                textureImporter.filterMode = FilterMode.Point;
                textureImporter.isReadable = true;
                // 重新导入资源
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
        GUILayout.BeginHorizontal();
        EditorGUIUtility.labelWidth = 50;
        spacing = EditorGUILayout.IntField("spacing", spacing, GUILayout.Width(100));
        if (GUILayout.Button("开始", GUILayout.Width(101)))
        {
            EditorCoroutineRunner.StartEditorCoroutine(ExportImages());
        }
        if (GUILayout.Button("取消", GUILayout.Width(101)))
        {
            isCancel = true;
        }
        if (GUILayout.Button("中断识别|生成小图", GUILayout.Width(120)))
        {
            isContinue = false;
        }
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        isShowMask = GUILayout.Toggle(isShowMask, "debug");
        EditorGUIUtility.labelWidth = 60;
        //viewSize = EditorGUILayout.FloatField("viewSize", viewSize);
        GUILayout.EndHorizontal();

        GUILayout.Label($"当前小图爬行像素次数 {list.Count}");
        GUILayout.Label($"识别到小图数 {listRect.Count}");
        GUILayout.BeginHorizontal();
        GUILayout.Label($"viewSize({viewSize.ToString("f1")})",GUILayout.Width(90));
        viewSize = GUILayout.HorizontalSlider(viewSize,0,3);
        GUILayout.EndHorizontal();
        
        offsetScrollview = GUILayout.BeginScrollView(offsetScrollview);
        if (bigImage)
        {
            float w = bigImage.width < position.width ? position.width : bigImage.width;
            float h = w * bigImage.height / bigImage.width;
            rUi = GUILayoutUtility.GetRect(w * viewSize, h * viewSize);
            rUi.width = w * viewSize;
            rUi.height = h * viewSize;
            GUI.DrawTexture(rUi, bigImage);//Graphics.DrawTexture(rUi, bigImage);
            if (isShowMask && mask)
            {
                GUI.DrawTexture(rUi,mask);//Graphics.DrawTexture(rUi, mask);
            }
        }
        GUILayout.EndScrollView();


    }

    void draw_debug_mask(bool isEnd=false)
    {
        if (!this.isShowMask&&!isEnd)
            return;
        if (!this.bigImage)
            return;
        if (visited != null)
        {
            if (!mask)
            {
                int w =Math.Clamp(bigImage.width/5, 200,500);
                int h =Math.Clamp(bigImage.height/5, 200,500);
                mask = new Texture2D(bigImage.width,bigImage.height);
            }

            for (int i = 0; i < mask.width; i++)
            {
                for (int j = 0; j < mask.height; j++)
                {
                    int x = (int)((float)bigImage.width / mask.width * i);
                    int y = (int)((float)bigImage.height / mask.height * j);
                    x = Mathf.Clamp(x, 0, bigImage.width - 1);
                    y = Mathf.Clamp(y, 0, bigImage.height - 1);
                    mask.SetPixel(i, j, visited[x, y].color);
                }
            }
            mask.Apply();
        }
    }
    bool isContinue=true;
    VisitItem[,] visited=null;
    public class VisitItem
    {
        public bool isVisit = false;
        public Color color= new Color(1, 0, 0, 0);
        public VisitItem()
        {
            isVisit = false;
        }
    }
    RectInt rect0= new RectInt(0,0,0,0);

    public IEnumerator ExportImages()
    {
        if (bigImage == null)
        {
            Debug.LogError("BigImage is null!");
            yield break;
        }
        listRect.Clear();
        list.Clear();
        rect0= new RectInt(0,0,0,0);
        mask=null;
        visited = new VisitItem[bigImage.width, bigImage.height];
         for (int i = 0; i < bigImage.width; i++)
        {
            for (int j = 0; j < bigImage.height; j++)
            {
                visited[i,j] = new VisitItem();
            }
        }   
        Queue<Vector2Int> points = new Queue<Vector2Int>();
        bool isbreak=false;
        for (int i = 0; i < bigImage.width; i++)
        {
            for (int j = 0; j < bigImage.height; j++)
            {
                var rayColor = bigImage.GetPixel(i, j);
                if (rayColor.a == 0)
                    continue;
                bool hasRect = false;
                foreach (var item in listRect)
                {
                    if (Contains(item,new Vector2Int(i, j)))
                    {
                        hasRect = true;
                        break;
                    }
                }
                if (hasRect)
                {
                    continue;
                }
                else
                {
                    list.Clear();
                    rect0 = new RectInt(i, j, 1, 1);
                    rect0.xMax =i;
                    rect0.yMax =j;
                    points.Enqueue(new Vector2Int(i, j));
                    visited[i, j].isVisit = true;
                }


                // 开始区域生长
                while (points.Count > 0)
                {
                    if(isCancel)
                    {
                        isCancel=false;
                        yield break;
                    }
                    if(!isContinue)
                    {                        
                        break ;
                    }
                    Vector2Int p = points.Dequeue();
                   
                    Color currentColor = bigImage.GetPixel(p.x, p.y);                  
                    #region / points
                    if (currentColor.a > 0 || list.Count == 0||!visited[p.x, p.y].isVisit)
                    {
                        //填充该像素为选中颜色，或者将该像素加入到选区中
                        list.Add(p);  
                        visited[p.x, p.y].isVisit = true;
                        //8方向 检测生长                      
                        var listnewpos = GetVector2AroundList(bigImage.width, bigImage.height, this.spacing, p);
                        for (int k = 0; k < listnewpos.Count; k++)
                        {
                            Vector2Int newPos = listnewpos[k];
                            if (Contains(rect0, newPos) && !visited[newPos.x, newPos.y].isVisit)
                            {
                                points.Enqueue(new Vector2Int(newPos.x, newPos.y));
                                visited[newPos.x, newPos.y].color = new Color(1, 0, 0, 0.5f);
                                // if (bigImage.GetPixel(newPos.x, newPos.y).a > 0)
                                // {
                                //     visited[newPos.x, newPos.y].color = new Color(0, 0, 1, 1);
                                // }
                            }
                        }
                    }

                    if (points.Count == 0)
                    {
                        var rectListPos = GetRectangleEdgeCoordinates(rect0);
                        for (int k = 0; k < rectListPos.Count; k++)
                        {
                            Vector2Int newPos = rectListPos[k];
                            if (!visited[newPos.x, newPos.y].isVisit)
                            {
                                points.Enqueue(new Vector2Int(newPos.x, newPos.y));
                                if (k % 4 == 0)
                                    visited[newPos.x, newPos.y].color = new Color(0, 1, 0, 0.5f);
                            }
                        }
                        if (points.Count == 0)
                        {
                            for (int k = 0; k < rectListPos.Count; k++)
                            {
                                Vector2Int newPos = rectListPos[k];
                                //if (k % 6 == 0)
                                visited[newPos.x, newPos.y].color = new Color(1, 1, 1, 1f);
                            }
                            draw_debug_mask();
                            // 强制重新绘制窗口
                            Repaint();
                        }
                        yield return null;
                    }
                    #endregion
                    if (this.isShowMask && list.Count % 100 == 0)
                    {
                        yield return null;
                        
                        draw_debug_mask();
                        // 强制重新绘制窗口
                        Repaint();
                       
                    }
                    else if (list.Count % 999 == 0)
                    {
                        yield return null;
                        
                        draw_debug_mask();
                        // 强制重新绘制窗口
                        Repaint();
                    }
                }//while

                listRect.Add(rect0);
                yield return null;
                if (!isContinue)
                {
                    isbreak = true;
                    isContinue = true;
                }
                if (isCancel)
                {
                    isCancel = false;
                    yield break;
                }
                if (!this.isShowMask)
                    Repaint();

            }
            if (isbreak)
            {
                break;
            }
        }

        draw_debug_mask(true);
        Repaint();

        // 创建导出目录（如果不存在）
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (listRect.Count < 1)
        {
            Debug.LogError("没有切到一张小图");
            yield break;
        }
        int debugCount =0 ;
        foreach (var item in listRect)
        {
            RectInt dd=new RectInt(new Vector2Int(),new Vector2Int());
            var r = item;
            r.x = Math.Clamp(r.x, 0, bigImage.width - 1);
            r.y = Math.Clamp(r.y, 0, bigImage.height - 1);
            r.xMax = Math.Clamp(r.xMax + 1, 0, bigImage.width - 1);
            r.yMax = Math.Clamp(r.yMax + 1, 0, bigImage.height - 1);
            int w= r.xMax-r.x;
            int h= r.yMax-r.y;
            if (w < 3 || h < 3)
            {
                continue;
            }
            Texture2D pi = new Texture2D(w, h,TextureFormat.RGBA32,false);
            pi.SetPixels(bigImage.GetPixels(r.x, r.y, w, h, 0));
            pi.Apply();
            var path = Path.Combine(outputDirectory, $"{r.x}_{r.y}_{r.width}_{r.height}.png");
            //Debug.Log($"--------------- {path}_{item}");//(9,323,60,375)   (9,325) (105，401)
            SaveTexture(pi, path);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
            textureImporter.alphaIsTransparency = true;
            textureImporter.mipmapEnabled = false;
            textureImporter.npotScale = TextureImporterNPOTScale.None;
            // 重新导入资源
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
           
            debugCount++;
        }
        EditorUtility.DisplayDialog("切图工具", $"切图完成{debugCount}/{listRect.Count}个小图被生成(小于两像素跳过)", "ok");
        Debug.Log($"---------{debugCount}/{listRect.Count}个小图被生成(小于两像素跳过)");//(9,323,60,375)   (9,325) (105，401)
           
    }

    public bool Contains(RectInt rect,Vector2Int position)
    {
        return position.x >= rect.xMin && position.y >= rect.yMin && position.x <= rect.xMax && position.y <= rect.yMax;
    }
    public  List<Vector2Int> GetRectangleEdgeCoordinates(RectInt rect)
    {
        List<Vector2Int> coordinates = new List<Vector2Int>();
        Vector2Int p0=new Vector2Int(rect.x, rect.y);
        Vector2Int p1=new Vector2Int(rect.xMax, rect.yMax);
        // 遍历底边：从 (x0, y0) 到 (x1, y0)
        for (int x = p0.x; x <= p1.x; x++)
        {
            coordinates.Add(new Vector2Int(x, p0.y));
        }
        // 遍历右边：从 (x1, y0) 到 (x1, y1)
        for (int y = p0.y + 1; y <= p1.y; y++)
        {
            coordinates.Add(new Vector2Int(p1.x, y));
        }
        // 遍历上边：从 (x1, y1) 到 (x0, y1)
        for (int x = p1.x - 1; x >= p0.x; x--)
        {
            coordinates.Add(new Vector2Int(x, p1.y));
        }
        // 遍历左边：从 (x0, y1) 到 (x0, y0)
        for (int y = p1.y - 1; y > p0.y; y--)
        {
            coordinates.Add(new Vector2Int(p0.x, y));
        }        
        return coordinates;
    }
   List<Vector2Int> GetVector2AroundList(int width, int height, int padding, Vector2Int pos)
    {
        var list = new List<Vector2Int>();
        if (padding < 1) padding = 1;
        // 遍历 x 和 y 的所有可能值，范围是从 -padding 到 +padding
        for (int x = -padding; x <= padding; x++)
        {
            for (int y = -padding; y <= padding; y++)
            {
                if(Contains(rect0,new Vector2Int(pos.x + x, pos.y + y)))
                {
                    continue; 
                }
                if (pos.x + x >= 0 && pos.x + x < width && pos.y + y >= 0 && pos.y + y < height)
                {
                    list.Add(new Vector2Int(pos.x + x, pos.y + y));
                }
            }
        }
       
        for (int x = 0; x < list.Count; x++)
        {
            var newPos = list[x];
            if (bigImage.GetPixel(newPos.x, newPos.y).a > 0)
            {
                int xMax = rect0.xMax;
                int yMax = rect0.yMax;
                rect0.x = Math.Min(rect0.x, newPos.x);
                rect0.y = Math.Min(rect0.y, newPos.y);
                rect0.xMax = xMax;
                rect0.yMax = yMax;                

                rect0.xMax = Math.Max(rect0.xMax, newPos.x);
                rect0.yMax = Math.Max(rect0.yMax, newPos.y);
            }           
        }
        return list;
    }
  
   
    private void SaveTexture(Texture2D texture, string path)
    {
        if(File.Exists(path))
            File.Delete(path);
        byte[] bytes = texture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
    }
}