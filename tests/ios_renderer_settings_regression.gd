# SPDX-License-Identifier: Apache-2.0
extends SceneTree


func _init():
	# Reproduce the feature overrides used by the iOS export on a host machine.
	ProjectSettings.set_setting("_custom_features", "ios,mobile")
	var method = ProjectSettings.get_setting_with_override("rendering/renderer/rendering_method")
	if method != "gl_compatibility":
		push_error("iOS OpenGL initialization requires a Compatibility project setting, got " + str(method))
		quit(1)
		return
	print("IOS_RENDERER_SETTINGS_OK")
	quit()
