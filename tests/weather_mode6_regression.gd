extends SceneTree

# From the project root, run with a graphical renderer and --fixed-fps 60 or 120.
const SNOW_SHADER := preload("res://shader/weather_mode6.gdshader")
const SAMPLE_SIZE := Vector2i(320, 180)
const LIFETIME := 2.0 # Exercise synchronized engine re-emission twice.
const MAX_CHANGED_PIXELS := 4608

func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	if DisplayServer.get_name() == "headless":
		push_error("The mode 6 regression requires a graphical renderer.")
		quit(1)
		return

	var viewport := SubViewport.new()
	viewport.size = SAMPLE_SIZE
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	viewport.canvas_transform = Transform2D.IDENTITY.scaled(Vector2(0.25, 0.25))

	var background := ColorRect.new()
	background.size = Vector2(1280, 720)
	background.color = Color.BLACK
	viewport.add_child(background)

	var image := Image.create(32, 32, false, Image.FORMAT_RGBA8)
	image.fill(Color.WHITE)
	var texture := ImageTexture.create_from_image(image)
	_add_snow_layer(viewport, texture, 68, 1)
	_add_snow_layer(viewport, texture, 22, 2)

	var elapsed := 0.0
	var previous := PackedByteArray()
	var largest_change := 0
	var largest_change_time := 0.0
	var frames_with_snow := 0
	var frames_with_movement := 0
	while elapsed < LIFETIME * 2.5:
		await process_frame
		elapsed += root.get_process_delta_time()
		await RenderingServer.frame_post_draw
		if elapsed < 0.25:
			continue
		var sample := viewport.get_texture().get_image()
		sample.convert(Image.FORMAT_L8)
		var pixels := sample.get_data()
		var visible := 0
		var changed := 0
		for i in range(pixels.size()):
			if pixels[i] > 32:
				visible += 1
			if not previous.is_empty() and absi(pixels[i] - previous[i]) > 32:
				changed += 1
		if visible > 512:
			frames_with_snow += 1
		if changed > 64:
			frames_with_movement += 1
		if changed > largest_change:
			largest_change = changed
			largest_change_time = elapsed
		previous = pixels

	if largest_change > MAX_CHANGED_PIXELS or frames_with_snow < 30 or frames_with_movement < 30:
		push_error("Mode 6 snow continuity failed: max changed pixels=%d at %.3fs, visible frames=%d, moving frames=%d" %
			[largest_change, largest_change_time, frames_with_snow, frames_with_movement])
		quit(1)
		return
	print("WA2_WEATHER_MODE6_OK max_changed_pixels=%d visible_frames=%d moving_frames=%d" %
		[largest_change, frames_with_snow, frames_with_movement])
	quit()


func _add_snow_layer(viewport: SubViewport, texture: Texture2D, count: int, layer_mode: int) -> void:
	var material := ShaderMaterial.new()
	material.shader = SNOW_SHADER
	material.set_shader_parameter("speed_x", 3.0)
	material.set_shader_parameter("speed_y", 7.0)
	material.set_shader_parameter("thrbulence", 5.0)
	material.set_shader_parameter("large_layer_mode", layer_mode)

	var snow := GPUParticles2D.new()
	snow.amount = count
	snow.texture = texture
	snow.process_material = material
	snow.lifetime = LIFETIME
	snow.explosiveness = 1.0
	snow.use_fixed_seed = true
	snow.seed = 1263 + layer_mode
	# Match SetupSnowWeatherLayer: both layers share the simulation clock.
	snow.fixed_fps = 60
	snow.interpolate = true
	snow.fract_delta = false
	viewport.add_child(snow)
