太空工程师 mod 制作 总结 (更新中)
==================================

# 目录
- 创建一个简单mod
- 基础程序
- mod方块状态读取
- 读写custom data
- 操作其它方块
- 搜索API(雷达用)


# 创建一个简单MOD
- 参考
  - https://steamcommunity.com/sharedfiles/filedetails/?id=2672862655
  - 这篇指南的作者 是 _寂语不言丶_ 感谢他的整理
- workshop的mod的下载位置
  - D:\Steam\steamapps\workshop\content\244850
  - D:\Steam\steamapps\workshop\content\244850\1684690258
## 开发环境准备
- 参考上述文档

## 创建MOD模型
- 我们先采取导入官方模型, 再导出的方式, 先不自己建模
- 导入官方模型 参考上述文档
- 修正位置偏移
  - 不知道为什么, 有一些官方模型的mesh的位置是偏的
  - 框选需要调整的部分
  - 在上窗口的右上角 调整视角 比如Y轴从上往下看
  - 按G键进入移动模式, 拖动需要调整的多个组件到合适位置
  - 换一个视角, 比如X轴
  - 还是按G键, 调整Y轴偏移
  - 保存
- 删除不需要的部分
  - 例如 我要利用Beacon组件来做一个雷达MOD, 我把原beacon组件上的文字"BEACON"给删除了, 这样可以通过外观区分出来是我的MOD还是原版Beacon
- 其它模型修改 参考上述文档
- 导出模型 参考上述文档
  - 导出模型后, 记得改名, 不然会影响原版组件
  - 自己取个名字即可, 比如我的叫KRadar, 把导出的Beacon.mwm等, 全部改为KRadar.mwm即可

### Q&A
- Q: blender上部分截图对应的位置找不到
- A: 上下两个窗口的右侧边栏都有SEUT的子菜单, 注意两者都看一下.
     我这边blender的方面不熟, 仅提供一些最基础的操作tips

## 编写几个sbc文件
- 参考上述文档, 把对应的sbc文件补齐
  - 最好都补上, 使得我们的MOD方块可以研究, 可以生产.
  - 像方块分组等 原xml中是几组数据
  - 比如我想让我的雷达mod和原版天线放在一个组内
  - 在mod的sbc中, 其它几个分组都可以删除, 不影响原版分组
  - 仅在天线分组内, 重复其原始数据后, 添加我们自己的雷达MOD即可
  - 注意方块是由 TypeId和SubtypeId 共同指定

## 进游戏加载MOD并检查
- 检查内容如下
  - 原版方块是否一切正常
  - 新方块占用空间
    - 如果有错, 调整sbc中占用空间部分的x y z, 一般是轴搞错了, 比如beacon实际应该是Y轴2格大, 错搞成了Z轴2格大.
  - 新方块mount点
    - 至少有一个mount点, 能用就行了
  - 新方块mesh的偏移量
    - mesh错了, 见上述 模型-修正位置偏移 部分
  - 新方块G键分组
    - 分组sbc
  - 新方块研究树
    - 研究树 sbc
  - alt+F11 有没有debug报错
    - 此时一般不会报错

## 创建简单MOD 总结
- 现在我们已经创建好一个MOD 并加载到游戏中了
- 下一步 我们给该MOD增加脚本能力

# 基础程序

## 搭建编码环境
- 我以前都是用类似记事本的方法写脚本, 这次我们上visual studio.
- 在微软官网下载visual studio, 有C#能力即可
- 下载安装完成后, 打开, 创建工程
  - 有idea等ide使用经验的开发人员对这一步肯定不陌生了, 不详细说了
- 创建一个类库工程即可
- 引入Space Engineers(以下简称SE)的类库
  - 在工程的依赖项 部分, 右键 添加依赖项
  - 在弹窗中直接点击下面的浏览按键, 找到SE的安装目录, 加载bin64下的所有dll即可

## 创建入口类
- 入口有多个选择, 这里我们参考active radar这个mod的代码, 使用MySessionComponentBase基类
- 先找到active radar mod的代码
  - 在 steam安装目录(注意不是SE的安装目录), 找到 steamapps/workshop/content/244850目录
  - 244850即SE在steam的编号 这个目录下 每个ID即是一个工坊MOD
  - 在浏览器中打开工坊, 找到activa radar mod, 点进去后, URL中最后一个数字即是mod ID 为 1684690258
  - 打开后 找到 Data/Scripts/RadarBlock目录下的RadarCore.cs文件 参考该文件即可
- 先别全抄过来, 只抄一下引用 和 继承的部分
  - 自己起一个namespace 这个无所谓的
  - 自己起一个类名 无所谓
  - 继承 MySessionComponentBase
  - 把注解 [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)] 标上(不太清楚是否必要)
- 打印日志
  - 创建一个私有静态变量 类型为 TextWriter
  - 对该变量创建一个get方法封装
  - 在get方法中判断该变量为空则使用 MyAPIGateway.Utilities.WriteFileInLocalStorage 方法对其初始化
  - 不为空则返回已经创建好的TextWriter
  - 覆盖Init方法
    - 先调用base的Init(所有覆盖方法都别忘了这点)
  - 在Init方法中调用这个get拿到TextWriter后打印一行日志
  - 覆盖UnloadData方法
    - 对上述writer进行flush和close
- 检查日志输出
  - 如果一切正常 日志应输出在类似 AppData\Roaming\SpaceEngineers\Storage\KRadar_KRadar 目录下

### Q&A
- Q: 没看到日志
- A: 检查 alt+F11看是否有报错, 检查 AppData\Roaming\SpaceEngineers\ 目录下 SpaceEngineers_ 开头的日志有没有给出具体错误原因
- Q: 没找到类错误
- A: 检查using部分, 比如我之前就忘了using System.IO 导致找不到 TextWriter 类
- Q: 属性不能为 可空 类型的错误
- A: 这块具体为什么我也说不清楚, 总之不要使用TextWriter? 类型, 直接使用 TextWriter 类型即可 其实就算不带?也是可空的


# mod方块状态读取
- 创建mod方块监听对象
  - 基类 MyGameLogicComponent
  - 注解 MyEntityComponentDescriptor
- 监听mod方块的创建和关闭
- 形成全局List, 在Main(标注为MySessionComponentDescriptor)的UpdateBeforeSimulation中遍历处理每个mod方块
- 使用方块监听对象的Entity成员来获取本方块 强转为该方块的官方类型
  - 比如我们是基于beacon做的扩展, 这里就强转为IMyBeacon类型即可
  - 现在就可以使用IMyBeacon的相关方法了
  - 另外当然还可以使用IMyTerminalBlock的方法 因为IMyBeacon是IMyTerminalBlock的子类

# 存取customData
- 直接使用方块监听类的Entity, 转为IMyTerminalBlock 即可存取CustomData成员了
- 可以每2秒（120帧）存取一次, 减少负载 