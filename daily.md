# 0625
targetDict的长度为0

- shipPosition + v*t
- avoidPosition

- avoidPosition多源反推

- 1, 下坡 定速 定反向喷口会被定期关掉？usercontrol问题 done
- 2, 高速转向新版 避免摇头问题？


# 1005
- 指令设置模式 0 1 2
- 模式数据读取 msc名称 攻击中线，偏移角
  - 目前数据存储位置
    - RotorBase
  - 目前数据格式
    - 禁止开火范围 vert vertD vert2 vertD2 h hd h2 hd2 前后向，侧向
    - 待机位置 offX offY onX onY
    - 主攻轴线 ra 主攻范围 raD
  - 增加字段 TODO
    - msc名称
  - 改为数组格式，支持3级模式
- 切换模式指令 TODO

# 1006
- 增加msc 切换
- 切换模式

- 问题
  - 进入ra后，会先归到0位再开始瞄准？I太大了
  - 超过攻击范围后不是70 offYx？
  
- 增加arm l1 后处理，

- 问题
  - down模式手抬起来？加cfcsr 只有一个rotor需要控制fcsr

# 1007
- 问题
  - 下不灵了？
  - 人型态下 向上的喷口全开了？