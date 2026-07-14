/*******************************************************************************
The content of this file includes portions of the proprietary AUDIOKINETIC Wwise
Technology released in source code form as part of the game integration package.
The content of this file may not be used without valid licenses to the
AUDIOKINETIC Wwise Technology.
Note that the use of the game engine is subject to the Unity(R) Terms of
Service at https://unity3d.com/legal/terms-of-service
 
License Usage
 
Licensees holding valid licenses to the AUDIOKINETIC Wwise Technology may use
this file in accordance with the end user license agreement provided with the
software or, alternatively, in accordance with the terms contained
in a written agreement between you and Audiokinetic Inc.
Copyright (c) 2026 Audiokinetic Inc.
*******************************************************************************/

﻿#if UNITY_EDITOR_LINUX || (UNITY_STANDALONE_LINUX && !UNITY_EDITOR)
public partial class AkCommonUserSettings
{
	partial void SetSampleRate(AkPlatformInitSettings settings)
	{
		settings.uSampleRate = m_SampleRate;
	}
	
	protected partial string GetPluginPath()
	{
#if UNITY_EDITOR_LINUX
		return System.IO.Path.GetFullPath(AkUtilities.GetPathInPackage("Runtime/Plugins/Linux/x86_64/DSP"));
#else
		return System.IO.Path.Combine(UnityEngine.Application.dataPath, "Plugins" + System.IO.Path.DirectorySeparatorChar);		
#endif
	}
}
#endif

public class AkLinuxSettings : AkWwiseInitializationSettings.PlatformSettings
{
#if UNITY_EDITOR
	[UnityEditor.InitializeOnLoadMethod]
	private static void AutomaticPlatformRegistration()
	{
		if (UnityEditor.AssetDatabase.IsAssetImportWorkerProcess())
		{
			return;
		}

		RegisterPlatformSettingsClass<AkLinuxSettings>("Linux");
	}
#endif // UNITY_EDITOR
	
	protected override AkCommonUserSettings GetUserSettings()
	{
		return UserSettings;
	}

	protected override AkCommonAdvancedSettings GetAdvancedSettings()
	{
		return AdvancedSettings;
	}

	protected override AkCommonCommSettings GetCommsSettings()
	{
		return CommsSettings;
	}

	[System.Serializable]
	public class PlatformAdvancedSettings : AkCommonAdvancedSettings
	{
	}

	[UnityEngine.HideInInspector]
	public AkCommonUserSettings UserSettings;

	[UnityEngine.HideInInspector]
	public PlatformAdvancedSettings AdvancedSettings;

	[UnityEngine.HideInInspector]
	public AkCommonCommSettings CommsSettings;
}
