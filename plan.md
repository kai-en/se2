# 遮挡计算流程

1. grid无电过滤
2. 星球遮挡
3. 分辩率0.1过滤
  3.1 不过滤大信号（雷达视角半径超过0.1的信号）
  3.2 decoy信号处理
4. 大信号遮挡过滤
  4.1 decoy信号处理

# 星球遮挡

1. 星球遮挡放在最前？

# decoy优先

1. 在分辩率过滤阶段，decoy信号被认为距离雷达更近，更有可能被选中为代表信号
2. 在分辩率过滤阶段，decoy信号被认为有超过实际大小的默认半径（区分大小方块的decoy），更有可能被认为是大信号不被过滤
3. 在大信号遮挡阶段，decoy信号因为有更近的距离和默认半径，有可能遮挡住decoy释放源


# 其它问题 issues
1. 配置isEw=false 在 半径大于等于30000时必须为true 覆盖customData FIXED
2. 自身飞船没有正确遮挡 FIXED
  - grid的position并不是几何中心
  - 采用 grid.Physics.CenterOfMassWorld
3. 测试小行星的遮挡效果 TODO
4. 原版天线的通信能力时灵时不灵？

# update
## 0.9.1
- FIXED: crash when entity is null
- FEATURE: grid which has more than 20 terminal blocks will be a cover (no matter it has decoy on it or not)
- FIXED: signalRadius error
- FIXED: use grid.Physics.CenterOfMassWorld instead of grid.GetPosition()
- BALANCE: the grid which havs the radar on it will also cover some angle. So better place the KRadar on the edge of the grid. The cover radius is reduced. 

## 0.9.2
fix crash

## 0.9.3
add velocity

# other
- 导弹加油器进行分组，0组只找0组的gyro，1组只找1组的gyro，
  - gyro加参数 只找符合自己tag的
  - 加油器增加参数，1，找哪个分组的mergeblock，2，找merge的半径

- radar脚本输出目标列表（id + 是否高威 + 当前选中）, rgms读取该目标列表，
  - radar 脚本增加 循环选中目标功能，循环全部，最近高威
  - rgms 读取目标列表
  - rgms 每导弹跟踪一个目标ID，丢失目标后导弹自爆
  - 针对当前目标发射一颗导弹
  - 针对所有高威目标发射一颗导弹
  - 测试

- 导弹BUG
  - 发射正上飞 FIXED
  - 转向不足 gyro放宽 TEST
    - 检查转向误差 TEST
  - 基于AE的转向计算 TEST
  - 解除限速后精度不够 TEST
  - 近距模式连打了两发 TODO
  - 程序停止 TEST
    - prepare 数组越界 FIXED

- 导弹AE模式总结
  - 1，用debug模式，把推进器关掉，速度直接设定为向目标飞，专门调转向
  - 2，发现转向gyro的pitch是反的，需要加负号
  - 3，导弹有两级敏感度，第一级是rv（需要消除的速度）对应的角度的倍率，第二级是角度要求下达之后，Gyro转数的倍率
    - 如果是第一级P值过高，则无论如何调节第二级，导弹都只会打圈
  - 增加了方向不齐时减少喷射的设置 （DOT的平方）
    - 注意下限是0.2 否则可能导致慢速转圈的陷阱条件

- KRadar
  - 导弹会遮挡目标信号 DONE
    - 怀疑分辩率问题
    - 分辩率上有多个目标时，优先最大目标

- dcs
  - landing 二阶段折叠 FIXED

- rgms
  - 近距模式连打了两发 TEST

- fcsr
  - 目标观察器 DONE
  - 炮台读radar数据 DONE (KRadar 和 FCS 二选一 不能都用)
  - 当control时 
    - 提供 XY偏移量 DONE
    - 而不是直接失控 DONE
    - 换目标时清偏移量 DONE
    - 提供清偏移量指令 DONE ResetOffset
    - 有目标或无目标时显示/隐藏 瞄准框 DONE

- funnel
  - 每个加目标ID
  - 按当前选择发射

- 造型
  - 用舷窗
  - 霓虹灯

# DCS
- 分组最大满足模式 （理论依据 分组对称抵消副作用） DONE
  - 可动部分为 第一组
  - 氢推第二组 （节省氢气消耗）
  - 尾推第三组
  - 每组都最大满足当前要求 放宽角度要求到 只要有用就推？0.1倍投影以上推？
  - 产生的副作用加成到对后续组的要求上

# 参考资料
- mod blender
  - http://harag-on-steam.github.io/se-blender/
  - 改光照？

# mod block 接收事件
- 锁定偏移位置
- 参考 
  - https://forum.keenswh.com/threads/modders-can-now-create-their-own-terminal-interfaces-how.7384314/
- 查看是否每个KRadar都有Lock的Action DONE

# 设置偏移 读取偏移
- DONE

# 考虑Lock到子网格的问题？
- TODO

# 0220 bug
- 后摆问题？
  - 加日志
  - 加长后摆判定间隔，覆盖震荡 DONE
- 导弹首发命中有问题
  - 减小离舰高度 避免初始误差过大 DONE
  - 调整ATAN参数 0.7比较合适 0.5会振荡 1过于保守修正偏慢 DONE
  - 从瞄准镜看摆动是最简单的办法


# 0221
- 长期计划
  - FCSR 测试
  - RADAR加瞄准转向功能

- 磁轨炮炮台 FCSR
  - 射击精度 与 目标距离成正比 TEST
  - hinge正负反了 判断左右？TEST
  - 调参 正上方没射击？hd? TEST
  - 炮速 TEST
  - 下坠 TEST

- Radar 标记为已摧毁（不输出给FCSR） TEST 4号快捷键

- FCSR fire interval

- 防空炮 TEST
  - 弹速下调

- funnel
  - 预射出距离
  - 左右分开
    - 一侧被消灭 另一侧不会飞过去补
    - 优先发射不足的一侧


# 0402
- radar display 有不知来源的信号
  - 从母舰来的信号，关天线不能关掉信号，要关电
- fcsr不转
  - 进了瞄准台逻辑，即无武器时，又没有select的情况，之前是走attention, 现在改走searchStatbleDir

# 0409
- msc up offset_y


# 0429
- control模式 thrust不听控制？
- l2dam 在有俯仰时考虑俯仰
- down模式 矢量喷口没按下降1.5计算
- standby 模式关闭l2推力

pandeV2整合
演示看我的视频介绍
配置需求，750-2060系列显卡
百度网盘：https://pan.baidu.com/s/17cpm4E0C4XpRxDUKDwCjvg?pwd=khru
夸克网盘：https://pan.quark.cn/s/c1a9766b8c1c
解压码pande1871976531

# 0506
- 导弹解锁conn done
- 导弹refueler conn
- 测试原版雷达 done

# 0507
- 导弹refueler conn done


# 0512
coco 3ba slider presets?
https://www.nexusmods.com/skyrimspecialedition/mods/63816

手柄一键重击？
https://www.nexusmods.com/skyrimspecialedition/mods/72417

coco 浪人
http://www.9damaogames.com/forum.php?mod=viewthread&tid=218629&highlight=coco%2B%C0%CB%C8%CB

- 转向角速度调整 TODO