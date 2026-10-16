#ifndef WMV_PLAYER_H
#define WMV_PLAYER_H

#include <godot_cpp/classes/audio_stream_generator.hpp>
#include <godot_cpp/classes/audio_stream_generator_playback.hpp>
#include <godot_cpp/classes/audio_stream_player.hpp>
#include <godot_cpp/classes/image_texture.hpp>
#include <godot_cpp/classes/texture_rect.hpp>
#include <godot_cpp/variant/packed_byte_array.hpp>
#include <godot_cpp/variant/string.hpp>

#include <atomic>
#include <condition_variable>
#include <cstdint>
#include <deque>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace godot {

class WMVPlayer : public TextureRect {
	GDCLASS(WMVPlayer, TextureRect);

public:
	enum PlaybackState {
		STATE_STOPPED,
		STATE_LOADING,
		STATE_PLAYING,
		STATE_PAUSED,
	};

private:
	static constexpr int OUTPUT_SAMPLE_RATE = 48000;
	static constexpr size_t MAX_VIDEO_FRAMES = 18;
	static constexpr size_t AUDIO_PREBUFFER_FRAMES = OUTPUT_SAMPLE_RATE;
	static constexpr size_t VIDEO_PREBUFFER_FRAMES = 3;
	static constexpr int64_t MAX_AUDIO_FRAMES = OUTPUT_SAMPLE_RATE * 4;

	struct VideoFrame {
		double pts = 0.0;
		double duration = 0.0;
		int width = 0;
		int height = 0;
		// Filled by the decoder thread and handed to Image without another copy.
		godot::PackedByteArray rgba;
	};

	struct AudioChunk {
		double pts = 0.0;
		int offset = 0;
		std::vector<float> stereo_samples;

		int frame_count() const {
			return static_cast<int>(stereo_samples.size() / 2);
		}
	};

	String source;
	bool autoplay = false;
	bool loop = false;
	float volume_db = 0.0f;
	StringName audio_bus = StringName("Master");

	PlaybackState playback_state = STATE_STOPPED;
	double stream_position = 0.0;
	double stream_length = 0.0;
	double clock_anchor_position = 0.0;
	uint64_t clock_anchor_usec = 0;
	double audio_clock_anchor_position = 0.0;
	double audio_feed_position = 0.0;
	bool pending_paused = false;
	bool failure_reported = false;

	int video_width = 0;
	int video_height = 0;
	bool stream_has_audio = false;

	AudioStreamPlayer *audio_player = nullptr;
	Ref<AudioStreamGenerator> audio_generator;
	Ref<AudioStreamGeneratorPlayback> audio_playback;
	Ref<ImageTexture> video_texture;

	std::thread decoder_thread;
	std::thread audio_decoder_thread;
	std::atomic<bool> abort_decoder{ false };
	std::mutex queue_mutex;
	std::condition_variable queue_condition;
	std::deque<VideoFrame> video_queue;
	std::deque<AudioChunk> audio_queue;
	int64_t queued_audio_frames = 0;

	bool decoder_ready = false;
	bool decoder_eof = false;
	bool audio_decoder_eof = false;
	bool decoder_failed = false;
	String decoder_error;
	double decoder_length = 0.0;
	double decoder_end_position = 0.0;
	int decoder_width = 0;
	int decoder_height = 0;
	bool decoder_has_audio = false;

	void _notification(int p_what);
	void ensure_audio_player();
	void process_decoder_status();
	void process_video(double p_clock);
	void process_audio(double p_clock);
	void process_end_of_stream(double p_clock);
	void display_frame(VideoFrame &&p_frame);

	void start_decoder(double p_position, bool p_paused);
	void stop_decoder(bool p_clear_queues = true);
	void decoder_main(std::string p_path, double p_start_position);
	void audio_decoder_main(std::string p_path, double p_start_position);
	bool enqueue_video(VideoFrame &&p_frame);
	bool enqueue_audio(AudioChunk &&p_chunk);
	void set_decoder_failure(const String &p_message);
	void clear_audio_output();
	double playback_clock() const;
	String resolve_source_path() const;
	bool validate_source(String &r_error) const;

protected:
	static void _bind_methods();

public:
	WMVPlayer();
	~WMVPlayer();

	void set_source(const String &p_source);
	String get_source() const;

	void set_autoplay(bool p_autoplay);
	bool has_autoplay() const;

	void set_loop(bool p_loop);
	bool has_loop() const;

	void set_volume_db(float p_volume_db);
	float get_volume_db() const;

	void set_audio_bus(const StringName &p_bus);
	StringName get_audio_bus() const;

	void play();
	void play_from_position(double p_position);
	void pause();
	void set_paused(bool p_paused);
	bool is_paused() const;
	void stop();
	bool is_playing() const;

	void set_stream_position(double p_position);
	double get_stream_position() const;
	double get_stream_length() const;
	PlaybackState get_playback_state() const;
	Vector2i get_video_size() const;
	bool has_audio() const;
	int get_audio_underrun_count() const;
};

} // namespace godot

VARIANT_ENUM_CAST(godot::WMVPlayer::PlaybackState);

#endif
