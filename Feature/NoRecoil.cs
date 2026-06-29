using Assets.Sources.Free.Data;
using SkyDome.Cfg;
using SkyDome.Entity;
using SkyDome.Feature.Legit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Spine.Unity.MeshGenerator;

namespace SkyDome.Feature
{
    public class NoRecoil : MonoBehaviour
    {
        private Vector2 vector;
        private Quaternion lastQua;

        private void Update()
        {
            if (Config.NoRecoil)
            {
                float punchPitch = PlayerUpdate.LocalEntity.Punch.x;
                float punchYaw = PlayerUpdate.LocalEntity.Punch.y;
                Contexts.sharedInstance.userCommand.input.Pitch -= 2f * (punchPitch - this.vector.x);
                Contexts.sharedInstance.userCommand.input.Yaw -= 2f * (punchYaw - this.vector.y);
                Camera.main.transform.Rotate(-this.vector.x - GameModelLocator.GetInstance().GameModel.ShakeAngleOffect.y, -this.vector.y - GameModelLocator.GetInstance().GameModel.ShakeAngleOffect.x, 0f);
                this.vector.x = punchPitch;
                this.vector.y = punchYaw;
            }
        }
        private void LateUpdate()
        {
            if (Config.NoRecoil && Aimbot._isActive && Contexts.sharedInstance.weapon.currentWeaponEntity.slot.Slot < 3 && Config.SmoothControl)
            {
                if (Input.GetMouseButton(0))
                {
                    Camera.main.transform.localRotation = Quaternion.Slerp(this.lastQua, Camera.main.transform.localRotation, Time.deltaTime * 11f);
                }
                this.lastQua = Camera.main.transform.localRotation;
            }
        }
    }
}
