using Assets.Sources.Components.Player;
using Assets.Sources.Components.Player.UnityObjects;
using SkyDome.Utilities;
using System;
using UnityEngine;

namespace SkyDome.Entity
{
    public class PlayerInfo
    {
        public readonly PlayerEntity _entity;

        public PlayerInfo(PlayerEntity playerEntity)
        {
            _entity = playerEntity ?? throw new ArgumentNullException(nameof(playerEntity));
        }

        // 实体唯一ID
        public int Id => _entity.GetId();
        // 客户端时间戳
        public int CilentTime => _entity.GetClientTime();
        // 距离本地摄像机的距离(米)
        public int Distance => PlayerUpdate.MainCamera != null ? (int)(Vector3.Distance(PlayerUpdate.MainCamera.transform.position, SSJJMath.VectorCoordConverter.SsjjToUnity(Position)) * 0.01f) : 0;
        // 玩家名字(清理空字符)
        public string PlayerName => _entity.basicInfo.Current.PlayerName?.TrimEnd('\0') ?? "";
        // 阵营ID (1:红 2:蓝)
        public int Team => _entity.basicInfo.Current.Team;
        // 角色模型名称
        public string Career => _entity.basicInfo.Current.Career;

        // 当前血量
        public float Hp => _entity.basicInfo.Current.Hp;
        // 最大血量
        public float MaxHp => _entity.basicInfo.Current.MaxHp;
        // 血量百分比
        public float HpPercent => MaxHp > 0 ? Hp / MaxHp : 0f;
        // 是否死亡
        public bool IsDead => _entity.basicInfo.Current.IsDead;

        // 是否携带C4
        public bool HasC4 => _entity.basicInfo.Current.HasC4;

        // 视野组件(用于判断开镜)
        public FovComponent Fov => _entity.fov;

        // 武器显示名称
        public string Weapon => _entity.currentWeapon.WeaponInfo.StringName;

        // 武器ID名
        public string CurrentWeaponName => _entity.basicInfo.Current.CurrentWeaponName;

        // 武器槽位ID
        public int CurrentWeaponId => _entity.basicInfo.Current.CurrentWeapon;

        // 武器类型ID(0.手枪 1.步枪 2.近战 3.投掷物 5.狙击枪 6.霰弹 10.机枪 12.冲锋枪)
        public int WeaponDetailType => _entity.currentWeapon.WeaponInfo.WeaponType;

        // 是否在地面
        public bool OnGround => _entity.OnGround();

        // 补偿预测后的世界坐标
        public Vector3 Position => _entity.GetCompenstatePos(_entity.fpos.Change.PosIndex);

        // 视角角度向量
        public Vector2 ViewPos => new Vector2(_entity.GetViewPitch(), _entity.GetViewYaw());

        // 视角俯仰角(上下)
        public float ViewPitch => _entity.basicInfo.Current.ViewPitch;

        // 视角偏航角(左右)
        public float ViewYaw => _entity.basicInfo.Current.ViewYaw;

        // 移动俯仰角
        public float MovePitch => _entity.basicInfo.Current.MovePitch;

        // 移动偏航角
        public float MoveYaw => _entity.basicInfo.Current.MoveYaw;

        // 后坐力抖动量
        public Vector2 Punch => new Vector2(_entity.GetPunchPitch(), _entity.GetPunchYaw());

        // 第三人称Unity对象(骨骼等)
        public ThirdPersonUnityObjectsComponent ThirdPersonUnityObjects => _entity.thirdPersonUnityObjects;

        // 移动组件(速度等)
        public MoveComponent Move => _entity.move;

        // 状态组件(姿态等)
        public StateComponent State => _entity.state;
    }
}
