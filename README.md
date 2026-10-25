# 全局变形演示（Unity 2022.3 / URP）

一个可直接运行的 OBJ 全局变形演示程序，包含 **锥化（Taper）**、**扭曲（Twist）**、**弯曲（Bend）** 和 **FFD 自由变形**。

> 本文件夹是演示代码与说明文档的整合副本，与 Unity 工程内 `Assets/Scripts/` 下的实现一致；工程内的原始文件保持原位，不影响运行。
>
> ⚠️ 请勿把本文件夹整体放进 `Assets/`，否则会与工程内同名脚本重复编译（类名冲突）。需要并入其他工程时，请将 `Scripts/` 中的文件复制到目标工程的 `Assets/` 下，并将 `StreamingAssets/` 中的示例模型放到目标工程的 `Assets/StreamingAssets/`。

## 文件夹内容

```text
全局变形演示/
├─ README.md                 本说明
├─ Scripts/
│  ├─ Deformation/           锥化、扭曲、弯曲、FFD 核心算法
│  ├─ Demo/                  演示主控制器、IMGUI 面板、网格与控制点显示
│  ├─ Editor/                编辑器菜单与算法自检
│  ├─ Viewer/                OBJ 显示与轨道相机（含演示所需改动）
│  └─ ObjLoader/             OBJ 解析与 Mesh 构建
└─ StreamingAssets/          内置示例模型 vase.obj、cube.obj
```

## 快速开始

1. 用 Unity 2022.3.62f3c1 打开原工程。
2. 打开 `Assets/Scenes/SampleScene.unity`，点击 **Play**。
3. 程序会自动创建演示控制器并加载内置花瓶模型 `Assets/StreamingAssets/vase.obj`，左侧显示操作面板。

> 也可以通过菜单 `Tools/全局变形演示/在场景中创建演示对象`，把演示控制器手动放进场景。

## 操作说明

### 模型导入

- 点击 **导入 OBJ…**：编辑器 Play 模式会打开 Unity 文件面板；Windows 独立程序会打开系统文件对话框。
- 也可以在路径输入框中填写绝对路径或 `StreamingAssets` 下的相对路径，点击 **加载**。
- 内置示例：`vase.obj`（带 8 条纵向花瓣棱，扭曲效果明显）与 `cube.obj`。
- 导入后自动按模型包围盒调整相机视角。

### 变形按钮

| 按钮 | 说明 |
| --- | --- |
| 原始 | 恢复原始网格，不叠加任何变形 |
| 锥化 Taper | 沿高度轴按高度线性缩放截面：底部不变，顶部缩放 `1 + 收放量` |
| 扭曲 Twist | 截面随高度旋转，底部到顶部的相对转角为“总扭转角” |
| 弯曲 Bend | 以模型中部为支点，将模型沿轴弯成一段圆弧 |
| FFD 自由变形 | 用包围盒生成的 `n × n × n` 控制点网格驱动变形 |

锥化/扭曲/弯曲都支持切换高度轴 X / Y / Z。所有变形每次都从**原始顶点**重新计算，不累积误差。

### FFD 的使用

1. 点击 **FFD 自由变形**。
2. 初始状态会自动根据模型局部包围盒生成 `n × n × n` 个控制点（默认 `3 × 3 × 3 = 27` 个）；每轴点数可在 2～6 之间调整。
3. 终止状态下有两种修改方式：
   - 在画面中点击控制点小球，按住左键直接拖动；
   - 在面板中用 X / Y / Z 滑杆精确修改选中控制点的坐标。
4. 模型会随控制点实时变化。橙色点表示已修改，黄色点表示当前选中，灰线是初始网格。
5. **全部控制点复位** 可回到 FFD 初始状态；**恢复原始模型** 可退出所有变形。

FFD 初始控制点坐标严格由包围盒均分得到：

```text
p(i,j,k) = lerp(bounds.min, bounds.max, (i,j,k) / (n-1))
```

顶点由周围 8 个控制点做三线性插值。未修改控制点时，映射是恒等变换，所以初始状态与原模型完全一致。

## 代码结构

| 文件 | 作用 |
| --- | --- |
| [DeformationTypes.cs](Scripts/Deformation/DeformationTypes.cs) | 变形模式、变形轴枚举 |
| [MeshDeformer.cs](Scripts/Deformation/MeshDeformer.cs) | 锥化、扭曲、弯曲的 CPU 顶点算法 |
| [FfdLattice.cs](Scripts/Deformation/FfdLattice.cs) | `n×n×n` 控制网格、三线性采样、控制点复位 |
| [GlobalDeformationDemo.cs](Scripts/Demo/GlobalDeformationDemo.cs) | 演示主控制器、模型加载、变形调度、FFD 交互 |
| [GlobalDeformationDemo.UI.cs](Scripts/Demo/GlobalDeformationDemo.UI.cs) | IMGUI 操作面板与中文字体适配 |
| [FfdLatticeView.cs](Scripts/Demo/FfdLatticeView.cs) | 控制网格线框和控制点小球的显示与拾取 |
| [FfdHandle.cs](Scripts/Demo/FfdHandle.cs) | 控制点拾取标记 |
| [NativeFileDialog.cs](Scripts/Demo/NativeFileDialog.cs) | 编辑器 / Windows 文件选择器 |
| [GlobalDeformationBootstrap.cs](Scripts/Demo/GlobalDeformationBootstrap.cs) | 进入 Play 时自动创建演示（免手动搭场景） |
| [ModelViewer.cs](Scripts/Viewer/ModelViewer.cs) | 原有 OBJ 加载器，新增加载完成事件 |
| [CameraOrbit.cs](Scripts/Viewer/CameraOrbit.cs) | 轨道相机，新增 UI/控制点输入屏蔽 |
| [ObjParser.cs](Scripts/ObjLoader/ObjParser.cs) | OBJ 解析，新增 UTF-8 BOM 兼容 |
| [MeshBuilder.cs](Scripts/ObjLoader/MeshBuilder.cs) | 由 OBJ 数据构建 Mesh |
| [ObjData.cs](Scripts/ObjLoader/ObjData.cs) | OBJ 数据容器 |
| [GlobalDeformationMenu.cs](Scripts/Editor/GlobalDeformationMenu.cs) | 编辑器菜单 |
| [DeformationSelfTest.cs](Scripts/Editor/DeformationSelfTest.cs) | 变形算法自检（菜单或批处理） |

## 自检

菜单：`Tools/全局变形演示/运行变形算法自检`。

自检会读取 `vase.obj`，验证：

- OBJ 解析和 Mesh 构建；
- 锥化底截面不变、顶截面按比例缩放；
- 扭曲保持到中轴距离、中点不动；
- 弯曲中点不动、所有输出有限；
- FFD 未修改控制点时是恒等映射、修改控制点后顶点跟随、控制点数为 `n³`。

命令行方式：

```bat
Unity.exe -batchmode -quit -projectPath "<工程路径>" ^
  -executeMethod ObjViewer.EditorTools.DeformationSelfTest.RunFromCommandLine ^
  -logFile "<日志路径>" -nographics
```

## 构建说明

工程使用 URP。运行材质在代码中通过 `Universal Render Pipeline/Lit` 创建；如果目标是其他渲染管线，会自动回退到 `Standard`。
运行时文件对话框目前对 Windows 独立程序提供原生支持，其他平台可直接在面板路径输入框中填写绝对路径。
