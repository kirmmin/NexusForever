using System;
using System.Collections.Generic;
using System.Text;

namespace NexusForever.WorldServer.Game.PathContent.Static
{
    public enum PathMissionType
    {
        Soldier_Holdout             = 0x0000,
        Scientist_Scan              = 0x0002,
        Explorer_Area               = 0x0003,
        Soldier_Assassinate         = 0x0004,
        Soldier_Demolition          = 0x0005,
        Soldier_RescueOp            = 0x0006,
        Soldier_Swat                = 0x0007,
        Soldier_Script              = 0x0008,
        Settler_Script              = 0x0009,
        Scientist_Script            = 0x000A,
        Explorer_Script             = 0x000B,
        Explorer_Door               = 0x000C,
        Explorer_ScavengerHunt      = 0x000D,
        Scientist_ScanChecklist     = 0x000E,
        Explorer_Vista              = 0x000F,
        Explorer_ExploreZone        = 0x0010,
        Explorer_ActivateChecklist  = 0x0011,
        Explorer_PowerMap           = 0x0012,
        Settler_Hub                 = 0x0013,
        Scientist_FieldStudy        = 0x0014,
        Settler_Infrastructure      = 0x0015,
        Scientist_Experimentation   = 0x0016,
        Scientist_SpecimenSurvey    = 0x0017,
        Scientist_DatacubeDiscovery = 0x0018,
        Settler_Mayor               = 0x0019,
        Settler_Sheriff             = 0x001A,
        Settler_Scout               = 0x001B
    }
}
