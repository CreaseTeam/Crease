/////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Audiokinetic Wwise generated include file. Do not edit.
//
/////////////////////////////////////////////////////////////////////////////////////////////////////

#ifndef __WWISE_IDS_H__
#define __WWISE_IDS_H__

#include <AK/SoundEngine/Common/AkTypes.h>

namespace AK
{
    namespace EVENTS
    {
        static const AkUniqueID PLAY_AMBIENTPLAYGROUNDBOUNDARY = 723389955U;
        static const AkUniqueID PLAY_AMBIENTSWINGS = 2069913319U;
        static const AkUniqueID PLAY_AMBIENTWATERSPOUT = 827576222U;
        static const AkUniqueID PLAY_AMBIENTWINDFRUSTRUM1 = 1280143093U;
        static const AkUniqueID PLAY_AMBIENTWINDFRUSTRUM2 = 1280143094U;
        static const AkUniqueID PLAY_AMBIENTWINDFRUSTRUM3 = 1280143095U;
        static const AkUniqueID PLAY_AMBIENTWINDFRUSTRUMGENERAL = 2187218884U;
        static const AkUniqueID PLAY_CHECKPOINTREACHED = 711455078U;
        static const AkUniqueID PLAY_CLOSEMAILBOX = 2761942810U;
        static const AkUniqueID PLAY_COINCOLLECT = 979988297U;
        static const AkUniqueID PLAY_CREASEPAPER = 2906773315U;
        static const AkUniqueID PLAY_FLAGPOPUP = 3205025518U;
        static const AkUniqueID PLAY_FOLDPAPER = 3430616211U;
        static const AkUniqueID PLAY_HOVEROFF = 2449173295U;
        static const AkUniqueID PLAY_HOVERON = 2838845755U;
        static const AkUniqueID PLAY_PLANEDASH = 2481628620U;
        static const AkUniqueID PLAY_PLANESPEEDSOUNDS = 2220497489U;
        static const AkUniqueID PLAY_PLAYERBUBBLEESCAPE = 40743970U;
        static const AkUniqueID PLAY_PLAYERBUBBLETRAP = 3240092342U;
        static const AkUniqueID PLAY_PLAYERPOPBUBBLE = 447315244U;
        static const AkUniqueID PLAY_REGDAMAGE = 395496407U;
        static const AkUniqueID PLAY_REMOVESTICKER = 1789546543U;
        static const AkUniqueID PLAY_SELECTBUTTON = 348159706U;
        static const AkUniqueID PLAY_SELECTSTICKER = 953563835U;
        static const AkUniqueID PLAY_STICKERCOLLECT = 2321545575U;
        static const AkUniqueID PLAY_WATERDAMAGE = 94728852U;
        static const AkUniqueID PLAY_WINDFRUSTRUMLIFT = 1844555015U;
        static const AkUniqueID START_CRAYONSCRIBBLE = 3834105472U;
        static const AkUniqueID START_L1AMBREGIONSWITCH = 964996825U;
        static const AkUniqueID START_L1SUBREGIONSWITCH = 3237153347U;
        static const AkUniqueID START_PENCILSCRIBBLE = 3260018181U;
        static const AkUniqueID STOP_CRAYONSCRIBBLE = 1099243338U;
        static const AkUniqueID STOP_L1AMBREGIONSWITCH = 1116908779U;
        static const AkUniqueID STOP_L1SUBREGIONSWITCH = 3755980033U;
        static const AkUniqueID STOP_PENCILSCRIBBLE = 1795284375U;
    } // namespace EVENTS

    namespace STATES
    {
        namespace LV1MUSICREGIONSTATES
        {
            static const AkUniqueID GROUP = 1057141339U;

            namespace STATE
            {
                static const AkUniqueID CLOSEDFORESTTUNNEL = 1032506856U;
                static const AkUniqueID NONE = 748895195U;
                static const AkUniqueID OPENFORESTAREA = 1215672537U;
                static const AkUniqueID UNDERWATERPLAYGROUND = 357507693U;
                static const AkUniqueID WINDINGTUNNEL = 625350595U;
            } // namespace STATE
        } // namespace LV1MUSICREGIONSTATES

        namespace LV1REGIONAMBIENCESTATES
        {
            static const AkUniqueID GROUP = 4215095390U;

            namespace STATE
            {
                static const AkUniqueID FORESTAREA = 3336434501U;
                static const AkUniqueID GRASSLAKEAREA = 421983475U;
                static const AkUniqueID NONE = 748895195U;
            } // namespace STATE
        } // namespace LV1REGIONAMBIENCESTATES

        namespace LV1SUBREGIONAMBIENCESTATES
        {
            static const AkUniqueID GROUP = 1333243800U;

            namespace STATE
            {
                static const AkUniqueID GRASSRUSTLEAREA = 2273428829U;
                static const AkUniqueID NONE = 748895195U;
            } // namespace STATE
        } // namespace LV1SUBREGIONAMBIENCESTATES

    } // namespace STATES

    namespace SWITCHES
    {
        namespace PLAYER_DAMAGE_LEVEL
        {
            static const AkUniqueID GROUP = 1591838669U;

            namespace SWITCH
            {
                static const AkUniqueID HIGH = 3550808449U;
                static const AkUniqueID LOW = 545371365U;
                static const AkUniqueID MEDIUM = 2849147824U;
            } // namespace SWITCH
        } // namespace PLAYER_DAMAGE_LEVEL

    } // namespace SWITCHES

    namespace GAME_PARAMETERS
    {
        static const AkUniqueID PLAYER_DAMAGEINTENSITY = 783376577U;
        static const AkUniqueID PLAYER_FLIGHTSPEED = 4135936604U;
    } // namespace GAME_PARAMETERS

    namespace BUSSES
    {
        static const AkUniqueID MAIN_AUDIO_BUS = 2246998526U;
    } // namespace BUSSES

    namespace AUDIO_DEVICES
    {
        static const AkUniqueID NO_OUTPUT = 2317455096U;
        static const AkUniqueID SYSTEM = 3859886410U;
    } // namespace AUDIO_DEVICES

}// namespace AK

#endif // __WWISE_IDS_H__
