#include "wmv_player.h"

#include <godot_cpp/classes/file_access.hpp>
#include <godot_cpp/classes/image.hpp>
#include <godot_cpp/classes/time.hpp>
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/core/error_macros.hpp>
#include <godot_cpp/variant/packed_byte_array.hpp>
#include <godot_cpp/variant/packed_vector2_array.hpp>
#include <godot_cpp/variant/utility_functions.hpp>

extern "C" {
#include <libavcodec/avcodec.h>
#include <libavformat/avformat.h>
#include <libavutil/channel_layout.h>
#include <libavutil/error.h>
#include <libavutil/imgutils.h>
#include <libavutil/mathematics.h>
#include <libswresample/swresample.h>
#include <libswscale/swscale.h>
}

#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <limits>
#include <utility>

namespace godot {

namespace {

struct GodotFileReader {
	Ref<FileAccess> file;
};

int read_godot_file(void *p_opaque, uint8_t *p_buffer, int p_buffer_size) {
	GodotFileReader *reader = static_cast<GodotFileReader *>(p_opaque);
	if (reader == nullptr || reader->file.is_null()) {
		return AVERROR(EIO);
	}

	PackedByteArray data = reader->file->get_buffer(p_buffer_size);
	if (data.is_empty()) {
		return AVERROR_EOF;
	}
	std::memcpy(p_buffer, data.ptr(), data.size());
	return data.size();
}

int64_t seek_godot_file(void *p_opaque, int64_t p_offset, int p_whence) {
	GodotFileReader *reader = static_cast<GodotFileReader *>(p_opaque);
	if (reader == nullptr || reader->file.is_null()) {
		return AVERROR(EIO);
	}
	if (p_whence == AVSEEK_SIZE) {
		return reader->file->get_length();
	}

	p_whence &= ~AVSEEK_FORCE;
	int64_t target = p_offset;
	if (p_whence == SEEK_CUR) {
		target += reader->file->get_position();
	} else if (p_whence == SEEK_END) {
		target += reader->file->get_length();
	} else if (p_whence != SEEK_SET) {
		return AVERROR(EINVAL);
	}

	target = std::max<int64_t>(0, target);
	reader->file->seek(target);
	return reader->file->get_position();
}

String ffmpeg_error_string(int p_error) {
	char buffer[AV_ERROR_MAX_STRING_SIZE] = {};
	av_strerror(p_error, buffer, sizeof(buffer));
	return String::utf8(buffer);
}

double timestamp_seconds(const AVStream *p_stream, int64_t p_timestamp, double p_global_start) {
	if (p_timestamp == AV_NOPTS_VALUE) {
		return std::numeric_limits<double>::quiet_NaN();
	}
	return static_cast<double>(p_timestamp) * av_q2d(p_stream->time_base) - p_global_start;
}

} // namespace

void WMVPlayer::_bind_methods() {
	ClassDB::bind_method(D_METHOD("set_source", "path"), &WMVPlayer::set_source);
	ClassDB::bind_method(D_METHOD("get_source"), &WMVPlayer::get_source);
	ClassDB::bind_method(D_METHOD("set_autoplay", "enabled"), &WMVPlayer::set_autoplay);
	ClassDB::bind_method(D_METHOD("has_autoplay"), &WMVPlayer::has_autoplay);
	ClassDB::bind_method(D_METHOD("set_loop", "enabled"), &WMVPlayer::set_loop);
	ClassDB::bind_method(D_METHOD("has_loop"), &WMVPlayer::has_loop);
	ClassDB::bind_method(D_METHOD("set_volume_db", "volume_db"), &WMVPlayer::set_volume_db);
	ClassDB::bind_method(D_METHOD("get_volume_db"), &WMVPlayer::get_volume_db);
	ClassDB::bind_method(D_METHOD("set_audio_bus", "bus"), &WMVPlayer::set_audio_bus);
	ClassDB::bind_method(D_METHOD("get_audio_bus"), &WMVPlayer::get_audio_bus);

	ClassDB::bind_method(D_METHOD("play"), &WMVPlayer::play);
	ClassDB::bind_method(D_METHOD("play_from_position", "position"), &WMVPlayer::play_from_position);
	ClassDB::bind_method(D_METHOD("pause"), &WMVPlayer::pause);
	ClassDB::bind_method(D_METHOD("set_paused", "paused"), &WMVPlayer::set_paused);
	ClassDB::bind_method(D_METHOD("is_paused"), &WMVPlayer::is_paused);
	ClassDB::bind_method(D_METHOD("stop"), &WMVPlayer::stop);
	ClassDB::bind_method(D_METHOD("is_playing"), &WMVPlayer::is_playing);
	ClassDB::bind_method(D_METHOD("set_stream_position", "position"), &WMVPlayer::set_stream_position);
	ClassDB::bind_method(D_METHOD("get_stream_position"), &WMVPlayer::get_stream_position);
	ClassDB::bind_method(D_METHOD("get_stream_length"), &WMVPlayer::get_stream_length);
	ClassDB::bind_method(D_METHOD("get_playback_state"), &WMVPlayer::get_playback_state);
	ClassDB::bind_method(D_METHOD("get_video_size"), &WMVPlayer::get_video_size);
	ClassDB::bind_method(D_METHOD("has_audio"), &WMVPlayer::has_audio);
	ClassDB::bind_method(D_METHOD("get_audio_underrun_count"), &WMVPlayer::get_audio_underrun_count);

	ADD_GROUP("Playback", "");
	ADD_PROPERTY(PropertyInfo(Variant::STRING, "source", PROPERTY_HINT_FILE, "*.wmv,*.pak"), "set_source", "get_source");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "autoplay"), "set_autoplay", "has_autoplay");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "loop"), "set_loop", "has_loop");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "volume_db", PROPERTY_HINT_RANGE, "-80,24,0.1,suffix:dB"), "set_volume_db", "get_volume_db");
	ADD_PROPERTY(PropertyInfo(Variant::STRING_NAME, "audio_bus", PROPERTY_HINT_ENUM, "Master"), "set_audio_bus", "get_audio_bus");

	ADD_GROUP("Status", "");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "stream_position", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_READ_ONLY), "", "get_stream_position");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "stream_length", PROPERTY_HINT_NONE, "", PROPERTY_USAGE_READ_ONLY), "", "get_stream_length");
	ADD_PROPERTY(PropertyInfo(Variant::INT, "playback_state", PROPERTY_HINT_ENUM, "Stopped,Loading,Playing,Paused", PROPERTY_USAGE_READ_ONLY), "", "get_playback_state");

	ADD_SIGNAL(MethodInfo("playback_started"));
	ADD_SIGNAL(MethodInfo("playback_paused"));
	ADD_SIGNAL(MethodInfo("playback_stopped"));
	ADD_SIGNAL(MethodInfo("playback_finished"));
	ADD_SIGNAL(MethodInfo("playback_error", PropertyInfo(Variant::STRING, "message")));

	BIND_ENUM_CONSTANT(STATE_STOPPED);
	BIND_ENUM_CONSTANT(STATE_LOADING);
	BIND_ENUM_CONSTANT(STATE_PLAYING);
	BIND_ENUM_CONSTANT(STATE_PAUSED);
}

WMVPlayer::WMVPlayer() {
	set_expand_mode(TextureRect::EXPAND_IGNORE_SIZE);
	set_stretch_mode(TextureRect::STRETCH_KEEP_ASPECT_CENTERED);
	set_process(true);
}

WMVPlayer::~WMVPlayer() {
	stop_decoder();
}

void WMVPlayer::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_READY: {
			ensure_audio_player();
			if (autoplay && !source.is_empty()) {
				play();
			}
		} break;
		case NOTIFICATION_PROCESS: {
			if (playback_state == STATE_STOPPED) {
				return;
			}

			process_decoder_status();
			if (playback_state == STATE_STOPPED) {
				return;
			}

			const double clock = playback_clock();
			stream_position = clock;
			process_video(clock);
			if (playback_state == STATE_PLAYING) {
				process_audio(clock);
				process_end_of_stream(clock);
			}
		} break;
		case NOTIFICATION_EXIT_TREE: {
			stop_decoder();
			clear_audio_output();
			playback_state = STATE_STOPPED;
		} break;
	}
}

void WMVPlayer::ensure_audio_player() {
	if (audio_player != nullptr) {
		return;
	}

	audio_generator.instantiate();
	audio_generator->set_mix_rate_mode(AudioStreamGenerator::MIX_RATE_CUSTOM);
	audio_generator->set_mix_rate(static_cast<float>(OUTPUT_SAMPLE_RATE));
	audio_generator->set_buffer_length(2.0f);

	audio_player = memnew(AudioStreamPlayer);
	audio_player->set_name("WMVAudio");
	audio_player->set_stream(audio_generator);
	audio_player->set_volume_db(volume_db);
	audio_player->set_bus(audio_bus);
	add_child(audio_player, false, INTERNAL_MODE_BACK);
}

void WMVPlayer::process_decoder_status() {
	bool ready = false;
	bool failed = false;
	String error_message;
	double length = 0.0;
	int width = 0;
	int height = 0;
	bool audio = false;

	{
		std::lock_guard<std::mutex> lock(queue_mutex);
		if (decoder_ready) {
			const bool audio_prebuffered = !decoder_has_audio ||
					queued_audio_frames >= static_cast<int64_t>(AUDIO_PREBUFFER_FRAMES) || audio_decoder_eof;
			const bool video_prebuffered = video_queue.size() >= VIDEO_PREBUFFER_FRAMES || decoder_eof;
			ready = audio_prebuffered && video_prebuffered;
			if (ready) {
				decoder_ready = false;
			}
			length = decoder_length;
			width = decoder_width;
			height = decoder_height;
			audio = decoder_has_audio;
		}
		if (decoder_failed && !failure_reported) {
			failed = true;
			failure_reported = true;
			error_message = decoder_error;
		}
	}

	if (failed) {
		stop_decoder();
		clear_audio_output();
		playback_state = STATE_STOPPED;
		emit_signal("playback_error", error_message);
		UtilityFunctions::push_error(String("WMVPlayer: ") + error_message);
		return;
	}

	if (!ready) {
		return;
	}

	stream_length = std::max(0.0, length);
	video_width = width;
	video_height = height;
	stream_has_audio = audio;
	stream_position = clock_anchor_position;
	audio_feed_position = clock_anchor_position;

	ensure_audio_player();
	clear_audio_output();
	if (stream_has_audio) {
		audio_player->set_stream(audio_generator);
		audio_player->play();
		audio_playback = audio_player->get_stream_playback();
		if (audio_playback.is_valid()) {
			process_audio(clock_anchor_position);
		}
		audio_clock_anchor_position = audio_player->get_playback_position();
		if (pending_paused) {
			audio_player->set_stream_paused(true);
		}
	}
	clock_anchor_usec = Time::get_singleton()->get_ticks_usec();

	playback_state = pending_paused ? STATE_PAUSED : STATE_PLAYING;
	emit_signal("playback_started");
}

void WMVPlayer::process_video(double p_clock) {
	VideoFrame selected;
	bool has_frame = false;
	{
		std::lock_guard<std::mutex> lock(queue_mutex);
		while (!video_queue.empty() && video_queue.front().pts <= p_clock + 0.015) {
			selected = std::move(video_queue.front());
			video_queue.pop_front();
			has_frame = true;
		}
	}

	if (has_frame) {
		queue_condition.notify_all();
		display_frame(std::move(selected));
	}
}

void WMVPlayer::process_audio(double) {
	if (!stream_has_audio || audio_playback.is_null()) {
		return;
	}

	while (true) {
		const int available = audio_playback->get_frames_available();
		if (available <= 0) {
			break;
		}

		PackedVector2Array output;
		int count = 0;
		bool silence = false;
		{
			std::lock_guard<std::mutex> lock(queue_mutex);
			if (audio_queue.empty()) {
				break;
			}

			AudioChunk &chunk = audio_queue.front();
			double chunk_position = chunk.pts + static_cast<double>(chunk.offset) / OUTPUT_SAMPLE_RATE;
			if (chunk_position < audio_feed_position) {
				const int overlap_frames = static_cast<int>(std::ceil(
						(audio_feed_position - chunk_position) * OUTPUT_SAMPLE_RATE));
				const int skipped = std::min(overlap_frames, chunk.frame_count() - chunk.offset);
				chunk.offset += skipped;
				queued_audio_frames -= skipped;
				chunk_position = chunk.pts + static_cast<double>(chunk.offset) / OUTPUT_SAMPLE_RATE;
			}

			const int remaining = chunk.frame_count() - chunk.offset;
			if (remaining <= 0) {
				audio_queue.pop_front();
				queue_condition.notify_all();
				continue;
			}

			const int gap_frames = static_cast<int>(std::llround(
					(chunk_position - audio_feed_position) * OUTPUT_SAMPLE_RATE));
			if (gap_frames > 1) {
				count = std::min({ available, gap_frames, 4096 });
				silence = true;
			} else {
				count = std::min({ available, remaining, 4096 });
			}
			output.resize(count);
			Vector2 *write = output.ptrw();
			if (silence) {
				for (int i = 0; i < count; ++i) {
					write[i] = Vector2();
				}
			} else {
				for (int i = 0; i < count; ++i) {
					const int sample = (chunk.offset + i) * 2;
					write[i] = Vector2(chunk.stereo_samples[sample], chunk.stereo_samples[sample + 1]);
				}
			}
		}

		if (count <= 0 || !audio_playback->push_buffer(output)) {
			break;
		}

		{
			std::lock_guard<std::mutex> lock(queue_mutex);
			if (!silence && !audio_queue.empty()) {
				AudioChunk &chunk = audio_queue.front();
				chunk.offset += count;
				queued_audio_frames -= count;
				if (chunk.offset >= chunk.frame_count()) {
					audio_queue.pop_front();
				}
			}
			audio_feed_position += static_cast<double>(count) / OUTPUT_SAMPLE_RATE;
		}
		queue_condition.notify_all();
	}
}

void WMVPlayer::process_end_of_stream(double p_clock) {
	bool eof = false;
	bool queues_empty = false;
	double end_position = 0.0;
	{
		std::lock_guard<std::mutex> lock(queue_mutex);
		eof = decoder_eof && (!stream_has_audio || audio_decoder_eof);
		queues_empty = video_queue.empty() && audio_queue.empty();
		end_position = decoder_end_position;
	}

	if (!eof || !queues_empty || p_clock + 0.01 < end_position) {
		return;
	}

	stop_decoder();
	clear_audio_output();
	stream_position = end_position;
	playback_state = STATE_STOPPED;
	emit_signal("playback_finished");

	if (loop && is_inside_tree()) {
		start_decoder(0.0, false);
	}
}

void WMVPlayer::display_frame(VideoFrame &&p_frame) {
	Ref<Image> image = Image::create_from_data(p_frame.width, p_frame.height, false, Image::FORMAT_RGBA8, p_frame.rgba);
	if (image.is_null() || image->is_empty()) {
		return;
	}

	if (video_texture.is_null() || video_texture->get_width() != p_frame.width || video_texture->get_height() != p_frame.height) {
		video_texture = ImageTexture::create_from_image(image);
		set_texture(video_texture);
	} else {
		video_texture->update(image);
	}
}

void WMVPlayer::start_decoder(double p_position, bool p_paused) {
	stop_decoder();

	p_position = std::max(0.0, p_position);
	if (stream_length > 0.0) {
		p_position = std::min(p_position, stream_length);
	}

	pending_paused = p_paused;
	failure_reported = false;
	stream_position = p_position;
	clock_anchor_position = p_position;
	clock_anchor_usec = Time::get_singleton()->get_ticks_usec();
	playback_state = STATE_LOADING;
	abort_decoder.store(false);

	{
		std::lock_guard<std::mutex> lock(queue_mutex);
		decoder_ready = false;
		decoder_eof = false;
		audio_decoder_eof = false;
		decoder_failed = false;
		decoder_error = String();
		decoder_end_position = p_position;
	}

	const String path = resolve_source_path();
	const CharString path_utf8 = path.utf8();
	decoder_thread = std::thread(&WMVPlayer::decoder_main, this, std::string(path_utf8.get_data()), p_position);
	audio_decoder_thread = std::thread(&WMVPlayer::audio_decoder_main, this, std::string(path_utf8.get_data()), p_position);
}

void WMVPlayer::stop_decoder(bool p_clear_queues) {
	abort_decoder.store(true);
	queue_condition.notify_all();
	if (decoder_thread.joinable()) {
		decoder_thread.join();
	}
	if (audio_decoder_thread.joinable()) {
		audio_decoder_thread.join();
	}

	if (p_clear_queues) {
		std::lock_guard<std::mutex> lock(queue_mutex);
		video_queue.clear();
		audio_queue.clear();
		queued_audio_frames = 0;
		decoder_ready = false;
		decoder_eof = false;
		audio_decoder_eof = false;
		decoder_failed = false;
	}
}

bool WMVPlayer::enqueue_video(VideoFrame &&p_frame) {
	std::unique_lock<std::mutex> lock(queue_mutex);
	queue_condition.wait(lock, [this]() {
		return abort_decoder.load() || video_queue.size() < MAX_VIDEO_FRAMES;
	});
	if (abort_decoder.load()) {
		return false;
	}
	decoder_end_position = std::max(decoder_end_position, p_frame.pts + p_frame.duration);
	video_queue.push_back(std::move(p_frame));
	return true;
}

bool WMVPlayer::enqueue_audio(AudioChunk &&p_chunk) {
	const int remaining = p_chunk.frame_count() - p_chunk.offset;
	if (remaining <= 0) {
		return true;
	}

	std::unique_lock<std::mutex> lock(queue_mutex);
	queue_condition.wait(lock, [this, remaining]() {
		return abort_decoder.load() || queued_audio_frames + remaining <= MAX_AUDIO_FRAMES;
	});
	if (abort_decoder.load()) {
		return false;
	}
	decoder_end_position = std::max(decoder_end_position, p_chunk.pts + static_cast<double>(p_chunk.frame_count()) / OUTPUT_SAMPLE_RATE);
	queued_audio_frames += remaining;
	audio_queue.push_back(std::move(p_chunk));
	return true;
}

void WMVPlayer::set_decoder_failure(const String &p_message) {
	if (abort_decoder.load()) {
		return;
	}
	{
		std::lock_guard<std::mutex> lock(queue_mutex);
		decoder_failed = true;
		decoder_error = p_message;
	}
	queue_condition.notify_all();
}

void WMVPlayer::clear_audio_output() {
	if (audio_player != nullptr) {
		audio_player->stop();
		audio_player->set_stream(Ref<AudioStream>());
	}
	if (audio_playback.is_valid()) {
		audio_playback.unref();
	}
}

double WMVPlayer::playback_clock() const {
	if (playback_state != STATE_PLAYING) {
		return stream_position;
	}
	if (stream_has_audio && audio_player != nullptr && audio_player->is_playing()) {
		const double audio_position = audio_player->get_playback_position();
		return clock_anchor_position + std::max(0.0, audio_position - audio_clock_anchor_position);
	}
	const uint64_t now = Time::get_singleton()->get_ticks_usec();
	return clock_anchor_position + static_cast<double>(now - clock_anchor_usec) / 1000000.0;
}

String WMVPlayer::resolve_source_path() const {
	return source;
}

bool WMVPlayer::validate_source(String &r_error) const {
	if (source.is_empty()) {
		r_error = "No WMV source file is configured.";
		return false;
	}
	if (!FileAccess::file_exists(source)) {
		r_error = "WMV file does not exist or cannot be opened: " + source;
		return false;
	}
	return true;
}

void WMVPlayer::decoder_main(std::string p_path, double p_start_position) {
	AVFormatContext *format_context = avformat_alloc_context();
	AVCodecContext *video_context = nullptr;
	AVPacket *packet = nullptr;
	AVFrame *frame = nullptr;
	AVIOContext *avio_context = nullptr;
	SwsContext *sws_context = nullptr;
	GodotFileReader file_reader;

	auto cleanup = [&]() {
		if (sws_context != nullptr) {
			sws_freeContext(sws_context);
		}
		if (frame != nullptr) {
			av_frame_free(&frame);
		}
		if (packet != nullptr) {
			av_packet_free(&packet);
		}
		if (video_context != nullptr) {
			avcodec_free_context(&video_context);
		}
		if (format_context != nullptr) {
			avformat_close_input(&format_context);
		}
		if (avio_context != nullptr) {
			av_freep(&avio_context->buffer);
			avio_context_free(&avio_context);
		}
		file_reader.file.unref();
	};

	auto fail = [&](const String &p_message) {
		cleanup();
		set_decoder_failure(p_message);
	};

	if (format_context == nullptr) {
		fail("Could not allocate the FFmpeg format context.");
		return;
	}
	format_context->interrupt_callback.callback = [](void *p_opaque) -> int {
		return static_cast<std::atomic<bool> *>(p_opaque)->load() ? 1 : 0;
	};
	format_context->interrupt_callback.opaque = &abort_decoder;

	file_reader.file = FileAccess::open(String::utf8(p_path.c_str()), FileAccess::READ);
	if (file_reader.file.is_null()) {
		fail("Godot could not open the WMV file for reading.");
		return;
	}
	constexpr int IO_BUFFER_SIZE = 64 * 1024;
	uint8_t *io_buffer = static_cast<uint8_t *>(av_malloc(IO_BUFFER_SIZE));
	if (io_buffer == nullptr) {
		fail("Could not allocate the FFmpeg IO buffer.");
		return;
	}
	avio_context = avio_alloc_context(io_buffer, IO_BUFFER_SIZE, 0, &file_reader,
			read_godot_file, nullptr, seek_godot_file);
	if (avio_context == nullptr) {
		av_free(io_buffer);
		fail("Could not initialize FFmpeg IO.");
		return;
	}
	format_context->pb = avio_context;
	format_context->flags |= AVFMT_FLAG_CUSTOM_IO;

	int result = avformat_open_input(&format_context, nullptr, nullptr, nullptr);
	if (result < 0) {
		fail(String("FFmpeg could not open the WMV file: ") + ffmpeg_error_string(result));
		return;
	}
	result = avformat_find_stream_info(format_context, nullptr);
	if (result < 0) {
		fail(String("FFmpeg could not read stream information: ") + ffmpeg_error_string(result));
		return;
	}
	if (format_context->iformat == nullptr || format_context->iformat->name == nullptr ||
			std::strcmp(format_context->iformat->name, "asf") != 0) {
		fail("Only WMV/ASF media content is supported, regardless of the file extension.");
		return;
	}

	const int video_index = av_find_best_stream(format_context, AVMEDIA_TYPE_VIDEO, -1, -1, nullptr, 0);
	if (video_index < 0) {
		fail("The WMV file does not contain a decodable video stream.");
		return;
	}
	AVStream *video_stream = format_context->streams[video_index];
	const AVCodecID video_codec_id = video_stream->codecpar->codec_id;
	if (video_codec_id != AV_CODEC_ID_WMV1 && video_codec_id != AV_CODEC_ID_WMV2 &&
			video_codec_id != AV_CODEC_ID_WMV3 && video_codec_id != AV_CODEC_ID_VC1) {
		fail("The ASF file does not contain a supported WMV video stream.");
		return;
	}
	const AVCodec *video_codec = avcodec_find_decoder(video_stream->codecpar->codec_id);
	if (video_codec == nullptr) {
		fail("FFmpeg has no decoder for this WMV video codec.");
		return;
	}
	video_context = avcodec_alloc_context3(video_codec);
	if (video_context == nullptr || avcodec_parameters_to_context(video_context, video_stream->codecpar) < 0) {
		fail("Could not initialize the WMV video decoder.");
		return;
	}
	result = avcodec_open2(video_context, video_codec, nullptr);
	if (result < 0) {
		fail(String("Could not open the WMV video decoder: ") + ffmpeg_error_string(result));
		return;
	}

	const int audio_index = av_find_best_stream(format_context, AVMEDIA_TYPE_AUDIO, -1, video_index, nullptr, 0);
	AVStream *audio_stream = audio_index >= 0 ? format_context->streams[audio_index] : nullptr;
	const bool has_decodable_audio = audio_stream != nullptr &&
			avcodec_find_decoder(audio_stream->codecpar->codec_id) != nullptr;

	packet = av_packet_alloc();
	frame = av_frame_alloc();
	if (packet == nullptr || frame == nullptr) {
		fail("Could not allocate FFmpeg decode buffers.");
		return;
	}

	const double global_start = format_context->start_time == AV_NOPTS_VALUE ? 0.0 :
			static_cast<double>(format_context->start_time) / AV_TIME_BASE;
	double length = format_context->duration == AV_NOPTS_VALUE ? 0.0 :
			static_cast<double>(format_context->duration) / AV_TIME_BASE;
	if (length <= 0.0 && video_stream->duration != AV_NOPTS_VALUE) {
		length = static_cast<double>(video_stream->duration) * av_q2d(video_stream->time_base);
	}

	if (p_start_position > 0.0) {
		const int64_t seek_target = static_cast<int64_t>((p_start_position + global_start) * AV_TIME_BASE);
		result = av_seek_frame(format_context, -1, seek_target, AVSEEK_FLAG_BACKWARD);
		if (result < 0) {
			fail(String("Could not seek in the WMV file: ") + ffmpeg_error_string(result));
			return;
		}
		avcodec_flush_buffers(video_context);
	}

	double video_rate = av_q2d(video_stream->avg_frame_rate);
	if (!std::isfinite(video_rate) || video_rate <= 0.0) {
		video_rate = 30.0;
	}
	double next_video_pts = p_start_position;

	auto output_video_frame = [&]() -> bool {
		double pts = timestamp_seconds(video_stream, frame->best_effort_timestamp, global_start);
		if (!std::isfinite(pts)) {
			pts = next_video_pts;
		}
		pts = std::max(0.0, pts);
		double duration = frame->duration > 0 ? frame->duration * av_q2d(video_stream->time_base) : 1.0 / video_rate;
		if (!std::isfinite(duration) || duration <= 0.0) {
			duration = 1.0 / video_rate;
		}
		next_video_pts = pts + duration;
		if (pts + duration < p_start_position - 0.001) {
			return true;
		}

		sws_context = sws_getCachedContext(sws_context, frame->width, frame->height,
				static_cast<AVPixelFormat>(frame->format), frame->width, frame->height,
				AV_PIX_FMT_RGBA, SWS_BILINEAR, nullptr, nullptr, nullptr);
		if (sws_context == nullptr) {
			return false;
		}

		VideoFrame output;
		output.pts = pts;
		output.duration = duration;
		output.width = frame->width;
		output.height = frame->height;
		output.rgba.resize(static_cast<int64_t>(frame->width) * frame->height * 4);
		uint8_t *destination[4] = { output.rgba.ptrw(), nullptr, nullptr, nullptr };
		int destination_stride[4] = { frame->width * 4, 0, 0, 0 };
		sws_scale(sws_context, frame->data, frame->linesize, 0, frame->height, destination, destination_stride);
		return enqueue_video(std::move(output));
	};

	auto drain_decoder = [&](AVCodecContext *p_context) -> bool {
		while (!abort_decoder.load()) {
			const int receive_result = avcodec_receive_frame(p_context, frame);
			if (receive_result == AVERROR(EAGAIN) || receive_result == AVERROR_EOF) {
				return true;
			}
			if (receive_result < 0) {
				return false;
			}
			const bool ok = output_video_frame();
			av_frame_unref(frame);
			if (!ok) {
				return false;
			}
		}
		return false;
	};

	bool decode_ok = true;
	{
		std::lock_guard<std::mutex> lock(queue_mutex);
		decoder_length = length;
		decoder_width = video_context->width;
		decoder_height = video_context->height;
		decoder_has_audio = has_decodable_audio;
		decoder_ready = true;
		decoder_end_position = std::max(decoder_end_position, p_start_position);
	}
	queue_condition.notify_all();

	while (!abort_decoder.load()) {
		result = av_read_frame(format_context, packet);
		if (result < 0) {
			break;
		}

		if (packet->stream_index == video_index) {
			result = avcodec_send_packet(video_context, packet);
			if (result >= 0 && !drain_decoder(video_context)) {
				decode_ok = false;
			}
		}
		av_packet_unref(packet);
		if (!decode_ok) {
			break;
		}
	}

	if (!abort_decoder.load() && decode_ok) {
		avcodec_send_packet(video_context, nullptr);
		decode_ok = drain_decoder(video_context);
	}

	const bool aborted = abort_decoder.load();
	cleanup();
	if (aborted) {
		return;
	}
	if (!decode_ok) {
		set_decoder_failure("FFmpeg failed while decoding the WMV stream.");
		return;
	}

	{
		std::lock_guard<std::mutex> lock(queue_mutex);
		decoder_eof = true;
		if (decoder_end_position <= p_start_position && length > 0.0) {
			decoder_end_position = length;
		}
	}
	queue_condition.notify_all();
}

void WMVPlayer::audio_decoder_main(std::string p_path, double p_start_position) {
	AVFormatContext *format_context = avformat_alloc_context();
	AVCodecContext *audio_context = nullptr;
	AVPacket *packet = nullptr;
	AVFrame *frame = nullptr;
	AVIOContext *avio_context = nullptr;
	SwrContext *swr_context = nullptr;
	AVChannelLayout stereo_layout = AV_CHANNEL_LAYOUT_STEREO;
	GodotFileReader file_reader;

	auto cleanup = [&]() {
		if (swr_context != nullptr) {
			swr_free(&swr_context);
		}
		if (frame != nullptr) {
			av_frame_free(&frame);
		}
		if (packet != nullptr) {
			av_packet_free(&packet);
		}
		if (audio_context != nullptr) {
			avcodec_free_context(&audio_context);
		}
		if (format_context != nullptr) {
			avformat_close_input(&format_context);
		}
		if (avio_context != nullptr) {
			av_freep(&avio_context->buffer);
			avio_context_free(&avio_context);
		}
		file_reader.file.unref();
		av_channel_layout_uninit(&stereo_layout);
	};

	auto fail = [&](const String &p_message) {
		cleanup();
		set_decoder_failure(p_message);
	};

	if (format_context == nullptr) {
		fail("Could not allocate the FFmpeg audio format context.");
		return;
	}
	format_context->interrupt_callback.callback = [](void *p_opaque) -> int {
		return static_cast<std::atomic<bool> *>(p_opaque)->load() ? 1 : 0;
	};
	format_context->interrupt_callback.opaque = &abort_decoder;

	file_reader.file = FileAccess::open(String::utf8(p_path.c_str()), FileAccess::READ);
	if (file_reader.file.is_null()) {
		fail("Godot could not open the WMV file for audio decoding.");
		return;
	}
	constexpr int IO_BUFFER_SIZE = 64 * 1024;
	uint8_t *io_buffer = static_cast<uint8_t *>(av_malloc(IO_BUFFER_SIZE));
	if (io_buffer == nullptr) {
		fail("Could not allocate the FFmpeg audio IO buffer.");
		return;
	}
	avio_context = avio_alloc_context(io_buffer, IO_BUFFER_SIZE, 0, &file_reader,
			read_godot_file, nullptr, seek_godot_file);
	if (avio_context == nullptr) {
		av_free(io_buffer);
		fail("Could not initialize FFmpeg audio IO.");
		return;
	}
	format_context->pb = avio_context;
	format_context->flags |= AVFMT_FLAG_CUSTOM_IO;

	int result = avformat_open_input(&format_context, nullptr, nullptr, nullptr);
	if (result < 0 || avformat_find_stream_info(format_context, nullptr) < 0) {
		fail("FFmpeg could not read the WMV audio stream.");
		return;
	}

	const int audio_index = av_find_best_stream(format_context, AVMEDIA_TYPE_AUDIO, -1, -1, nullptr, 0);
	if (audio_index < 0) {
		cleanup();
		{
			std::lock_guard<std::mutex> lock(queue_mutex);
			audio_decoder_eof = true;
		}
		queue_condition.notify_all();
		return;
	}
	AVStream *audio_stream = format_context->streams[audio_index];
	const AVCodec *audio_codec = avcodec_find_decoder(audio_stream->codecpar->codec_id);
	if (audio_codec == nullptr) {
		fail("FFmpeg has no decoder for the WMV audio codec.");
		return;
	}

	audio_context = avcodec_alloc_context3(audio_codec);
	if (audio_context == nullptr || avcodec_parameters_to_context(audio_context, audio_stream->codecpar) < 0 ||
			avcodec_open2(audio_context, audio_codec, nullptr) < 0) {
		fail("Could not initialize the WMV audio decoder.");
		return;
	}

	AVChannelLayout input_layout = {};
	const int input_channels = audio_context->ch_layout.nb_channels > 0 ?
			audio_context->ch_layout.nb_channels : audio_stream->codecpar->ch_layout.nb_channels;
	if (input_channels <= 0) {
		fail("The WMV audio stream has no valid channel layout.");
		return;
	}
	if (audio_context->ch_layout.order == AV_CHANNEL_ORDER_UNSPEC) {
		av_channel_layout_default(&input_layout, input_channels);
	} else {
		av_channel_layout_copy(&input_layout, &audio_context->ch_layout);
	}
	result = swr_alloc_set_opts2(&swr_context, &stereo_layout, AV_SAMPLE_FMT_FLT, OUTPUT_SAMPLE_RATE,
			&input_layout, audio_context->sample_fmt, audio_context->sample_rate, 0, nullptr);
	av_channel_layout_uninit(&input_layout);
	if (result < 0 || swr_context == nullptr || swr_init(swr_context) < 0) {
		fail("Could not initialize the WMV audio resampler.");
		return;
	}

	packet = av_packet_alloc();
	frame = av_frame_alloc();
	if (packet == nullptr || frame == nullptr) {
		fail("Could not allocate FFmpeg audio decode buffers.");
		return;
	}

	const double global_start = format_context->start_time == AV_NOPTS_VALUE ? 0.0 :
			static_cast<double>(format_context->start_time) / AV_TIME_BASE;
	if (p_start_position > 0.0) {
		const int64_t seek_target = static_cast<int64_t>((p_start_position + global_start) * AV_TIME_BASE);
		result = av_seek_frame(format_context, -1, seek_target, AVSEEK_FLAG_BACKWARD);
		if (result < 0) {
			fail(String("Could not seek in the WMV audio stream: ") + ffmpeg_error_string(result));
			return;
		}
		avcodec_flush_buffers(audio_context);
	}

	constexpr double AUDIO_DISCONTINUITY_THRESHOLD = 0.25;
	double next_audio_pts = p_start_position;
	bool audio_timeline_started = false;
	auto output_audio_frame = [&]() -> bool {
		double source_pts = timestamp_seconds(audio_stream, frame->best_effort_timestamp, global_start);
		const bool has_source_pts = std::isfinite(source_pts);
		if (has_source_pts) {
			source_pts = std::max(0.0, source_pts);
		}
		const int output_capacity = static_cast<int>(av_rescale_rnd(
				swr_get_delay(swr_context, audio_context->sample_rate) + frame->nb_samples,
				OUTPUT_SAMPLE_RATE, audio_context->sample_rate, AV_ROUND_UP));
		if (output_capacity <= 0) {
			return true;
		}

		AudioChunk output;
		output.stereo_samples.resize(static_cast<size_t>(output_capacity) * 2);
		uint8_t *destination[1] = { reinterpret_cast<uint8_t *>(output.stereo_samples.data()) };
		const int converted = swr_convert(swr_context, destination, output_capacity,
				const_cast<const uint8_t **>(frame->extended_data), frame->nb_samples);
		if (converted < 0) {
			return false;
		}
		output.stereo_samples.resize(static_cast<size_t>(converted) * 2);
		const double duration = static_cast<double>(converted) / OUTPUT_SAMPLE_RATE;
		double output_pts = next_audio_pts;
		if (!audio_timeline_started && has_source_pts) {
			output_pts = source_pts;
		} else if (audio_timeline_started && has_source_pts &&
				std::abs(source_pts - next_audio_pts) > AUDIO_DISCONTINUITY_THRESHOLD) {
			output_pts = source_pts;
		}
		next_audio_pts = output_pts + duration;
		if (!audio_timeline_started && next_audio_pts < p_start_position - 0.001) {
			return true;
		}
		output.pts = output_pts;
		if (!audio_timeline_started && output_pts < p_start_position) {
			output.offset = std::min(converted, static_cast<int>(
					std::ceil((p_start_position - output_pts) * OUTPUT_SAMPLE_RATE)));
		}
		if (output.offset >= converted) {
			return true;
		}
		audio_timeline_started = true;
		return enqueue_audio(std::move(output));
	};

	auto drain_audio_decoder = [&]() -> bool {
		while (!abort_decoder.load()) {
			const int receive_result = avcodec_receive_frame(audio_context, frame);
			if (receive_result == AVERROR(EAGAIN) || receive_result == AVERROR_EOF) {
				return true;
			}
			if (receive_result < 0) {
				return false;
			}
			const bool ok = output_audio_frame();
			av_frame_unref(frame);
			if (!ok) {
				return false;
			}
		}
		return false;
	};

	bool decode_ok = true;
	while (!abort_decoder.load()) {
		result = av_read_frame(format_context, packet);
		if (result < 0) {
			break;
		}
		if (packet->stream_index == audio_index) {
			result = avcodec_send_packet(audio_context, packet);
			if (result >= 0 && !drain_audio_decoder()) {
				decode_ok = false;
			}
		}
		av_packet_unref(packet);
		if (!decode_ok) {
			break;
		}
	}

	if (!abort_decoder.load() && decode_ok) {
		avcodec_send_packet(audio_context, nullptr);
		decode_ok = drain_audio_decoder();
	}

	const bool aborted = abort_decoder.load();
	cleanup();
	if (aborted) {
		return;
	}
	if (!decode_ok) {
		set_decoder_failure("FFmpeg failed while decoding the WMV audio stream.");
		return;
	}

	{
		std::lock_guard<std::mutex> lock(queue_mutex);
		audio_decoder_eof = true;
	}
	queue_condition.notify_all();
}

void WMVPlayer::set_source(const String &p_source) {
	if (source == p_source) {
		return;
	}
	if (playback_state != STATE_STOPPED) {
		stop();
	}
	source = p_source;
	stream_length = 0.0;
	stream_position = 0.0;
	video_width = 0;
	video_height = 0;
	stream_has_audio = false;
}

String WMVPlayer::get_source() const {
	return source;
}

void WMVPlayer::set_autoplay(bool p_autoplay) {
	autoplay = p_autoplay;
}

bool WMVPlayer::has_autoplay() const {
	return autoplay;
}

void WMVPlayer::set_loop(bool p_loop) {
	loop = p_loop;
}

bool WMVPlayer::has_loop() const {
	return loop;
}

void WMVPlayer::set_volume_db(float p_volume_db) {
	volume_db = p_volume_db;
	if (audio_player != nullptr) {
		audio_player->set_volume_db(volume_db);
	}
}

float WMVPlayer::get_volume_db() const {
	return volume_db;
}

void WMVPlayer::set_audio_bus(const StringName &p_bus) {
	audio_bus = p_bus;
	if (audio_player != nullptr) {
		audio_player->set_bus(audio_bus);
	}
}

StringName WMVPlayer::get_audio_bus() const {
	return audio_bus;
}

void WMVPlayer::play() {
	if (playback_state == STATE_PAUSED) {
		set_paused(false);
		return;
	}
	if (playback_state == STATE_PLAYING || playback_state == STATE_LOADING) {
		return;
	}
	double start = stream_position;
	if (stream_length > 0.0 && start >= stream_length - 0.001) {
		start = 0.0;
	}
	play_from_position(start);
}

void WMVPlayer::play_from_position(double p_position) {
	String error_message;
	if (!validate_source(error_message)) {
		emit_signal("playback_error", error_message);
		UtilityFunctions::push_error(String("WMVPlayer: ") + error_message);
		return;
	}
	ensure_audio_player();
	start_decoder(p_position, false);
}

void WMVPlayer::pause() {
	set_paused(true);
}

void WMVPlayer::set_paused(bool p_paused) {
	if (playback_state == STATE_LOADING) {
		pending_paused = p_paused;
		return;
	}
	if (p_paused && playback_state == STATE_PLAYING) {
		stream_position = playback_clock();
		playback_state = STATE_PAUSED;
		pending_paused = true;
		if (audio_player != nullptr && stream_has_audio) {
			audio_player->set_stream_paused(true);
		}
		emit_signal("playback_paused");
	} else if (!p_paused && playback_state == STATE_PAUSED) {
		clock_anchor_position = stream_position;
		clock_anchor_usec = Time::get_singleton()->get_ticks_usec();
		if (audio_player != nullptr && stream_has_audio) {
			audio_clock_anchor_position = audio_player->get_playback_position();
		}
		playback_state = STATE_PLAYING;
		pending_paused = false;
		if (audio_player != nullptr && stream_has_audio) {
			audio_player->set_stream_paused(false);
		}
	}
}

bool WMVPlayer::is_paused() const {
	return playback_state == STATE_PAUSED || (playback_state == STATE_LOADING && pending_paused);
}

void WMVPlayer::stop() {
	const bool was_active = playback_state != STATE_STOPPED;
	stop_decoder();
	clear_audio_output();
	playback_state = STATE_STOPPED;
	pending_paused = false;
	stream_position = 0.0;
	clock_anchor_position = 0.0;
	if (was_active) {
		emit_signal("playback_stopped");
	}
}

bool WMVPlayer::is_playing() const {
	return playback_state == STATE_PLAYING || (playback_state == STATE_LOADING && !pending_paused);
}

void WMVPlayer::set_stream_position(double p_position) {
	p_position = std::max(0.0, p_position);
	if (stream_length > 0.0) {
		p_position = std::min(p_position, stream_length);
	}
	if (playback_state == STATE_STOPPED) {
		stream_position = p_position;
		clock_anchor_position = p_position;
		return;
	}
	start_decoder(p_position, is_paused());
}

double WMVPlayer::get_stream_position() const {
	return playback_clock();
}

double WMVPlayer::get_stream_length() const {
	return stream_length;
}

WMVPlayer::PlaybackState WMVPlayer::get_playback_state() const {
	return playback_state;
}

Vector2i WMVPlayer::get_video_size() const {
	return Vector2i(video_width, video_height);
}

bool WMVPlayer::has_audio() const {
	return stream_has_audio;
}

int WMVPlayer::get_audio_underrun_count() const {
	return audio_playback.is_valid() ? audio_playback->get_skips() : 0;
}

} // namespace godot
