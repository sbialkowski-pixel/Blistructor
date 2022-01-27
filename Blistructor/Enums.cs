﻿using System.ComponentModel;
using System;
using System.Reflection;
using System.Linq;

namespace Blistructor
{
    public enum AnchorSite { JAW_1 = 0, JAW_2 = 1, Unset = 2 };
    public enum AnchorState { Active = 0, Inactive = 1, Cutted = 2 };

    public enum PillState { Queue = 0, Cutted = 1, Alone = 2 };
    public enum CutState { Failed = 0, Succeed = 1, Last = 2 };

    public enum CuttingState
    {
        [Description("Cutting successful")]
        CTR_SUCCESS = 0,
        [Description("Pills are to tight. Cutting aborted.")]
        CTR_TO_TIGHT = 1,
        [Description("One Outline on blister only. Nothing to do.")]
        CTR_ONE_PILL = 2,
        [Description("Cutting Failed. Cannot Found cutting paths for all pills. Blister is to complicated or it is uncuttable.")]
        CTR_FAILED = 3,
        [Description("Blister side to small to pick by both graspers or No place for graspers.")]
        CTR_ANCHOR_LOCATION_ERR = 4,
        [Description("Other Error. Check log file")]
        CTR_OTHER_ERR = 5,
        [Description("No blister")]
        CTR_NO_BLISTER  = 6,
        [Description("Blister badly aligned.")]
        CTR_WRONG_BLISTER_POSSITION = 7,
        [Description("Unknown")]
        CTR_UNSET = -1
    };


    public static class EnumExtensionMethods
    {
        public static string GetDescription(this Enum GenericEnum)
        {
            Type genericEnumType = GenericEnum.GetType();
            MemberInfo[] memberInfo = genericEnumType.GetMember(GenericEnum.ToString());
            if ((memberInfo != null && memberInfo.Length > 0))
            {
                var _Attribs = memberInfo[0].GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
                if ((_Attribs != null && _Attribs.Count() > 0))
                {
                    return ((System.ComponentModel.DescriptionAttribute)_Attribs.ElementAt(0)).Description;
                }
            }
            return GenericEnum.ToString();
        }

    }


}
