using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// Procedural TDM blockout arenas. Menu: Blockout/...
public static class BlockoutBuilder {
static Material ground,wall,floor,cover,dark,red,blue,cR,cB,cG,cY; static Transform root,cur; static List<float[]> nogo=new List<float[]>();
static void EnsureFolder(string parent, string name){ if(!AssetDatabase.IsValidFolder(parent+"/"+name)) AssetDatabase.CreateFolder(parent,name); }
static Material M(string n,float r,float g,float b){ string p="Assets/Blockout/Materials/"+n+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(p); if(m==null){ m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor",new Color(r,g,b)); m.SetFloat("_Smoothness",0.1f); AssetDatabase.CreateAsset(m,p);} return m; }
static void No(float cx,float cz,float hx,float hz){ nogo.Add(new float[]{cx,cz,hx,hz}); }
static GameObject B(string n,float x,float y0,float z,float sx,float sy,float sz,Material m,Transform par=null){ var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=n; g.transform.position=new Vector3(x,y0+sy/2,z); g.transform.localScale=new Vector3(sx,sy,sz); g.GetComponent<Renderer>().sharedMaterial=m; g.transform.SetParent(par!=null?par:cur,true); GameObjectUtility.SetStaticEditorFlags(g,(StaticEditorFlags)(-1)); return g; }
static void Grp(string n){ var g=new GameObject(n); g.transform.SetParent(root); cur=g.transform; }
static float[] D(float c,float w=3f){ return new float[]{c,w,0f,2.6f}; }
static float[] Wn(float c,float w=3f){ return new float[]{c,w,1f,2.5f}; }
static float[] Gt(float c,float w,float top){ return new float[]{c,w,0f,top}; }
static void Wall(string n,bool ax,float fix,float a0,float a1,float y0,float h,float t,Material m,params float[][] gaps){
  var gl=new System.Collections.Generic.List<float[]>(gaps); gl.Sort((p,q)=>p[0].CompareTo(q[0]));
  void Seg(float s,float e,float yy,float hh){ if(e-s<0.01f||hh<0.01f) return; float c=(s+e)/2,l=e-s; if(ax) B(n,c,yy,fix,l,hh,t,m); else B(n,fix,yy,c,t,hh,l,m); }
  float p0=a0;
  foreach(var g in gl){ float gs=g[0]-g[1]/2, ge=g[0]+g[1]/2; Seg(p0,gs,y0,h); Seg(gs,ge,y0,g[2]); Seg(gs,ge,y0+g[3],h-g[3]); p0=ge; }
  Seg(p0,a1,y0,h);
}
static void Stairs(string n,float sx,float sz,float y0,int dx,int dz,int steps,float width,float rise,float run){ for(int i=0;i<steps;i++){ float c=i*run+run/2; float px=sx+dx*c, pz=sz+dz*c; float hh=(i+1)*rise; if(dx!=0) B(n+i,px,y0,pz,run,hh,width,floor); else B(n+i,px,y0,pz,width,hh,run,floor); } }
static void Hut(string n,float cx,float cz,float w,float d,float h,int side,bool roof){ float x0=cx-w/2,x1=cx+w/2,z0=cz-d/2,z1=cz+d/2;
  Wall(n+"_N",true,z1,x0,x1,0,h,.5f,wall, side==0?new float[][]{D(cx)}:new float[0][]);
  Wall(n+"_S",true,z0,x0,x1,0,h,.5f,wall, side==1?new float[][]{D(cx)}:new float[0][]);
  Wall(n+"_E",false,x1,z0,z1,0,h,.5f,wall, side==2?new float[][]{D(cz)}:new float[0][]);
  Wall(n+"_W",false,x0,z0,z1,0,h,.5f,wall, side==3?new float[][]{D(cz)}:new float[0][]);
  if(roof) B(n+"_Roof",cx,h,cz,w+.5f,.3f,d+.5f,floor); No(cx,cz,w/2+1.5f,d/2+1.5f); }
static void HutP(string n,float cx,float cz,float w,float d,float h,int side,bool roof){ Hut(n+"A",cx,cz,w,d,h,side,roof); Hut(n+"B",-cx,-cz,w,d,h,side^1,roof); }
static void Cont(string n,float x,float y0,float z,bool alongX,Material m){ if(alongX) B(n,x,y0,z,6,2.6f,2.4f,m); else B(n,x,y0,z,2.4f,2.6f,6,m); }
static void ContP(string n,float x,float z,bool alongX,Material m,bool stack){ Cont(n+"A",x,0,z,alongX,m); Cont(n+"B",-x,0,-z,alongX,m); if(stack){ Cont(n+"A_top",x,2.6f,z,alongX,m); } if(alongX) {No(x,z,3.2f,1.6f);No(-x,-z,3.2f,1.6f);} else {No(x,z,1.6f,3.2f);No(-x,-z,1.6f,3.2f);} }
static void Scatter(int seed,int n,float xmax,float zmax,float exX,float exZ){ var r=new System.Random(seed); int placed=0,tries=0;
  bool Blocked(float x,float z){ if(Mathf.Abs(x)<exX&&Mathf.Abs(z)<exZ) return true; if(Mathf.Abs(z)>33||Mathf.Abs(x)>41) return true; foreach(var q in nogo){ if(Mathf.Abs(x-q[0])<q[2]+2f&&Mathf.Abs(z-q[1])<q[3]+2f) return true; } return false; }
  while(placed<n&&tries<800){ tries++; float x=(float)(r.NextDouble()*2-1)*xmax, z=(float)(r.NextDouble()*2-1)*zmax; if(Blocked(x,z)||Blocked(-x,-z)) continue; int k=r.Next(3);
    for(int s=0;s<2;s++){ float px=s==0?x:-x, pz=s==0?z:-z; if(k==0) B("Crate",px,0,pz,2,2,2,cover); else if(k==1) B("LowWall",px,0,pz,4,1.2f,.6f,cover); else B("Barrier",px,0,pz,.6f,1.2f,4,cover); }
    No(x,z,2,2); No(-x,-z,2,2); placed++; } }
static void Begin(){ nogo.Clear();
EnsureFolder("Assets","Blockout"); EnsureFolder("Assets/Blockout","Materials"); EnsureFolder("Assets/Scenes","Blockout");
ground=M("Ground",.33f,.37f,.33f); wall=M("Wall",.72f,.72f,.70f); floor=M("Floor",.5f,.5f,.55f); cover=M("Cover",.8f,.55f,.25f); dark=M("Dark",.2f,.2f,.22f);
red=M("TeamRed",.85f,.2f,.2f); blue=M("TeamBlue",.2f,.4f,.9f);
cR=M("ContainerRed",.7f,.2f,.15f); cB=M("ContainerBlue",.2f,.35f,.65f); cG=M("ContainerGreen",.2f,.5f,.3f); cY=M("ContainerYellow",.8f,.7f,.2f);
var sc=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,UnityEditor.SceneManagement.NewSceneMode.Single);
root=new GameObject("Blockout").transform; cur=root;
Grp("Arena");
B("Ground",0,-1,0,90,1,90,ground);
Wall("Perim_S",true,-44.5f,-45,45,0,5,1,dark); Wall("Perim_N",true,44.5f,-45,45,0,5,1,dark); Wall("Perim_W",false,-44.5f,-45,45,0,5,1,dark); Wall("Perim_E",false,44.5f,-45,45,0,5,1,dark);
foreach(var s in new[]{-1,1}){ var tm=s<0?red:blue; string nm=s<0?"Red":"Blue"; var g=new GameObject("Spawns_"+nm); g.transform.SetParent(root);
  for(int i=0;i<4;i++){ float x=-9+i*6; var sp=new GameObject("Spawn"+nm+"_"+(i+1)); sp.transform.SetParent(g.transform); sp.transform.position=new Vector3(x,1,s*38); sp.transform.rotation=Quaternion.Euler(0,s<0?0:180,0); B("Pad"+nm+i,x,0,s*38,3,.05f,3,tm,g.transform); } }
}
static void Finish(string name){ var cam=Camera.main; if(cam!=null){ cam.transform.position=new Vector3(0,75,-65); cam.transform.rotation=Quaternion.Euler(50,0,0);} var l=Object.FindAnyObjectByType<Light>(); if(l!=null) l.transform.rotation=Quaternion.Euler(50,-30,0); EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),"Assets/Scenes/Blockout/"+name+".unity"); Debug.Log("[Blockout] saved "+name+" ("+root.GetComponentsInChildren<Transform>().Length+" objects)"); }
[MenuItem("Blockout/TDM_01_Warehouse")] public static void L1(){ Begin();
Grp("Warehouse"); float t=.6f;
Wall("S_Low",true,-9,-12,12,0,4,t,wall,D(6)); Wall("N_Low",true,9,-12,12,0,4,t,wall,D(-6));
Wall("W_Low",false,-12,-9,9,0,4,t,wall,D(0)); Wall("E_Low",false,12,-9,9,0,4,t,wall,D(0));
B("Slab_A",0,4,4,24,.4f,10,floor); B("Slab_B",-11,4,-5,2,.4f,8,floor); B("Slab_C",3,4,-5,18,.4f,8,floor);
Stairs("Stair",-8,-8.5f,0,0,1,15,4f,4.4f/15,.5f);
Wall("S_Up",true,-9,-12,12,4.4f,3.2f,t,wall,Wn(-8),Wn(0),Wn(8)); Wall("N_Up",true,9,-12,12,4.4f,3.2f,t,wall,Wn(-8),Wn(0),Wn(8));
Wall("W_Up",false,-12,-9,9,4.4f,3.2f,t,wall,Wn(-4),Wn(4)); Wall("E_Up",false,12,-9,9,4.4f,3.2f,t,wall,Wn(-4),Wn(4));
foreach(var sx in new[]{-6f,6f}) foreach(var sz in new[]{-3f,3f}) B("Pillar",sx,0,sz,1,4,1,wall);
B("UpCover",4,4.4f,2,3,1.2f,.6f,cover); B("UpCover",-4,4.4f,5,.6f,1.2f,3,cover); B("UpCover",7,4.4f,-4,.6f,1.2f,3,cover);
No(0,0,14,11);
ContP("Cont",-26,-12,true,cR,true); ContP("Cont",-26,10,false,cG,false); ContP("Cont",-16,-28,true,cB,false); ContP("Cont",14,-24,false,cY,true);
Scatter(11,9,40,30,15,12);

Finish("TDM_01_Warehouse"); }
[MenuItem("Blockout/TDM_02_CrossRuins")] public static void L2(){ Begin();
Grp("Tower");
B("TowerBase",0,0,0,12,3.6f,12,floor); No(0,0,13,13);
Stairs("St_S",0,-12,0,0,1,12,4,.3f,.5f); Stairs("St_N",0,12,0,0,-1,12,4,.3f,.5f); Stairs("St_W",-12,0,0,1,0,12,4,.3f,.5f); Stairs("St_E",12,0,0,-1,0,12,4,.3f,.5f);
Wall("P_S",true,-5.7f,-6,6,3.6f,1.2f,.6f,wall,Gt(0,4,1.2f)); Wall("P_N",true,5.7f,-6,6,3.6f,1.2f,.6f,wall,Gt(0,4,1.2f));
Wall("P_W",false,-5.7f,-6,6,3.6f,1.2f,.6f,wall,Gt(0,4,1.2f)); Wall("P_E",false,5.7f,-6,6,3.6f,1.2f,.6f,wall,Gt(0,4,1.2f));
B("Nest",0,3.6f,0,6,2.4f,6,wall); Stairs("St_Nest",5.7f,0,3.6f,-1,0,8,3,.3f,.33f);
B("NestCover",-1.5f,6,-1.5f,1,1.2f,1,cover); B("NestCover",1.5f,6,1.5f,1,1.2f,1,cover);
Grp("RuinArms");
Wall("Arm_E",true,0,18,40,0,3,.8f,wall,D(25),Wn(34,2.5f)); Wall("Arm_W",true,0,-40,-18,0,3,.8f,wall,D(-25),Wn(-34,2.5f));
Wall("Arm_N",false,0,18,30,0,3,.8f,wall,D(24)); Wall("Arm_S",false,0,-30,-18,0,3,.8f,wall,D(-24));
foreach(var sx in new[]{-1f,1f}) foreach(var sz in new[]{-1f,1f}){ float cx=sx*24,cz=sz*24;
  Wall("Ruin_X",true,cz,cx-5,cx+5,0,2.4f,.7f,wall,Wn(cx,2.5f)); Wall("Ruin_Z",false,cx-sx*5,cz-5,cz+5,0,2.4f,.7f,wall,Gt(cz,2.4f,1.4f)); B("Pillar",cx+sx*6,0,cz+sz*6,1.2f,3.4f,1.2f,wall); No(cx,cz,7,7); }
Scatter(22,10,40,30,16,16);

Finish("TDM_02_CrossRuins"); }
[MenuItem("Blockout/TDM_03_Compound")] public static void L3(){ Begin();
Grp("Compound");
Wall("Fence_S",true,-18,-18,18,0,3.5f,.8f,wall,Gt(0,4,3.5f),Wn(-10,2.5f),Wn(10,2.5f)); Wall("Fence_N",true,18,-18,18,0,3.5f,.8f,wall,Gt(0,4,3.5f),Wn(-10,2.5f),Wn(10,2.5f));
Wall("Fence_W",false,-18,-18,18,0,3.5f,.8f,wall,Gt(0,4,3.5f),Wn(-10,2.5f),Wn(10,2.5f)); Wall("Fence_E",false,18,-18,18,0,3.5f,.8f,wall,Gt(0,4,3.5f),Wn(-10,2.5f),Wn(10,2.5f));
Wall("A_S",true,-6,-14,-2,0,4,.6f,wall,Wn(-8)); Wall("A_N",true,6,-14,-2,0,4,.6f,wall,D(-8));
Wall("A_W",false,-14,-6,6,0,4,.6f,wall,D(0)); Wall("A_E",false,-2,-6,6,0,4,.6f,wall,D(0));
B("A_Roof",-8,4,0,12.6f,.4f,12.6f,floor);
Wall("A_ParS",true,-6.1f,-14.1f,-1.9f,4.4f,1,.4f,wall,Gt(-8,3.4f,1)); Wall("A_ParN",true,6.1f,-14.1f,-1.9f,4.4f,1,.4f,wall);
Wall("A_ParW",false,-14.1f,-6.1f,6.1f,4.4f,1,.4f,wall); Wall("A_ParE",false,-1.9f,-6.1f,6.1f,4.4f,1,.4f,wall,Gt(0,2.6f,1));
Stairs("A_Stair",-8,-13.8f,0,0,1,15,3,4.4f/15,.5f);
B("B_Block",11,0,0,6,4.4f,6,floor); Stairs("B_Stair",11,-10.5f,0,0,1,15,3,4.4f/15,.5f);
B("Bridge",3.15f,4,0,9.7f,.4f,2.4f,floor); B("BridgeRail",3.15f,4.4f,1.1f,9.7f,1,.2f,wall); B("BridgeRail",3.15f,4.4f,-1.1f,9.7f,1,.2f,wall);
Wall("B_ParS",true,-2.8f,8,14,4.4f,1,.4f,wall,Gt(11,3.4f,1)); Wall("B_ParN",true,2.8f,8,14,4.4f,1,.4f,wall);
Wall("B_ParW",false,8.2f,-3,3,4.4f,1,.4f,wall,Gt(0,2.6f,1)); Wall("B_ParE",false,13.8f,-3,3,4.4f,1,.4f,wall);
No(0,0,20,20);
HutP("Hut",-30,-22,8,6,3.2f,0,true); ContP("Cont",-26,-8,false,cB,false); ContP("Cont",-12,-30,true,cR,true);
Scatter(33,8,40,30,19,19);

Finish("TDM_03_Compound"); }
[MenuItem("Blockout/TDM_04_Ziggurat")] public static void L4(){ Begin();
Grp("Ziggurat");
float[] half={20,14,8,4}; float[] off={0,8,-8,0};
for(int k=0;k<4;k++){ float h=half[k],o=off[k],s=h+4,y=2*k;
  B("Tier"+k,0,y,0,h*2,2,h*2,floor);
  Stairs("StS"+k,o,-s,y,0,1,8,3.4f,.25f,.5f); Stairs("StN"+k,-o,s,y,0,-1,8,3.4f,.25f,.5f);
  Stairs("StW"+k,-s,o,y,1,0,8,3.4f,.25f,.5f); Stairs("StE"+k,s,-o,y,-1,0,8,3.4f,.25f,.5f);
  float e=h-.2f,top=y+2;
  Wall("ParS"+k,true,-e,-h,h,top,1.1f,.4f,wall,Gt(o,3.8f,1.1f)); Wall("ParN"+k,true,e,-h,h,top,1.1f,.4f,wall,Gt(-o,3.8f,1.1f));
  Wall("ParW"+k,false,-e,-h,h,top,1.1f,.4f,wall,Gt(o,3.8f,1.1f)); Wall("ParE"+k,false,e,-h,h,top,1.1f,.4f,wall,Gt(-o,3.8f,1.1f)); }
B("SummitCover",-2,8,-2,1.2f,1.2f,1.2f,cover); B("SummitCover",2,8,2,1.2f,1.2f,1.2f,cover);
No(0,0,28,28);
ContP("Cont",-33,-10,false,cG,false);
Scatter(44,12,40,30,27,27);

Finish("TDM_04_Ziggurat"); }
[MenuItem("Blockout/TDM_05_Docks")] public static void L5(){ Begin();
Grp("Gantry");
foreach(var lx in new[]{-8f,8f}) foreach(var lz in new[]{-4.5f,4.5f}) B("Leg",lx-.75f,0,lz-.75f,1.5f,8,1.5f,dark);
B("Deck",0,8,0,20,.5f,12,floor);
Stairs("G_StS",0,-19.3f,0,0,1,28,4,8.5f/28,.475f); Stairs("G_StN",0,19.3f,0,0,-1,28,4,8.5f/28,.475f);
Wall("G_RailS",true,-5.9f,-10,10,8.5f,1.1f,.2f,dark,Gt(0,4.4f,1.1f)); Wall("G_RailN",true,5.9f,-10,10,8.5f,1.1f,.2f,dark,Gt(0,4.4f,1.1f));
Wall("G_RailW",false,-9.9f,-6,6,8.5f,1.1f,.2f,dark); Wall("G_RailE",false,9.9f,-6,6,8.5f,1.1f,.2f,dark);
B("DeckCover",-5,8.5f,0,.6f,1.2f,3,cover); B("DeckCover",5,8.5f,0,.6f,1.2f,3,cover);
Hut("Control",0,0,8,6,3.2f,2,true); No(0,0,14,22);
Grp("Containers");
foreach(var x in new[]{-27f,-15f,3f,15f,27f}) ContP("RowA",x,-31,true,(x<0?cR:cB),x==3f||x==-27f);
foreach(var x in new[]{-21f,-9f,9f,21f}) ContP("RowB",x,-23,true,(x<0?cG:cY),x==9f);
Scatter(55,6,40,30,24,10);

Finish("TDM_05_Docks"); }
[MenuItem("Blockout/Build All 5")] public static void BuildAll(){ L1(); L2(); L3(); L4(); L5();
  var list=new List<EditorBuildSettingsScene>(); foreach(var n in new[]{"TDM_01_Warehouse","TDM_02_CrossRuins","TDM_03_Compound","TDM_04_Ziggurat","TDM_05_Docks"}) list.Add(new EditorBuildSettingsScene("Assets/Scenes/Blockout/"+n+".unity",true)); EditorBuildSettings.scenes=list.ToArray(); }
}
