from __future__ import annotations

import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import dearpygui.dearpygui as dpg

from .loader import load_frame_sources
from .models import FrameAsset, PlaybackMode, SpriteSequence
from .playback import PlaybackState


@dataclass(slots=True)
class AppConfig:
    title: str = "Sprite Preview"
    viewport_width: int = 1280
    viewport_height: int = 860
    preview_width: int = 960
    preview_height: int = 640
    default_mode: PlaybackMode = "duration"
    default_fps: float = 6.0
    default_duration_seconds: float = 1.0
    default_loop: bool = True
    default_autoplay: bool = True


class SpritePreviewApp:
    def __init__(self, config: AppConfig | None = None) -> None:
        self.config = config or AppConfig()
        self.playback = PlaybackState(
            mode=self.config.default_mode,
            fps=self.config.default_fps,
            duration_seconds=self.config.default_duration_seconds,
            loop=self.config.default_loop,
            playing=False,
        )
        self.sequence: SpriteSequence | None = None
        self._load_generation = 0
        self._last_tick = 0.0
        self._autoplay_enabled = self.config.default_autoplay

        self.root_tag = "sprite_preview_root"
        self.texture_registry_tag = "sprite_preview_texture_registry"
        self.preview_drawlist_tag = "sprite_preview_drawlist"
        self.source_input_tag = "sprite_preview_source_input"
        self.mode_tag = "sprite_preview_mode"
        self.fps_tag = "sprite_preview_fps"
        self.duration_tag = "sprite_preview_duration"
        self.loop_tag = "sprite_preview_loop"
        self.autoplay_tag = "sprite_preview_autoplay"
        self.play_button_tag = "sprite_preview_play_button"
        self.status_tag = "sprite_preview_status"
        self.detail_tag = "sprite_preview_detail"
        self.frame_tag = "sprite_preview_frame"
        self.count_tag = "sprite_preview_count"
        self.dialog_tag = "sprite_preview_directory_dialog"

    def run(self, initial_source: Path | None = None) -> None:
        dpg.create_context()
        try:
            self._build_ui()
            dpg.create_viewport(title=self.config.title, width=self.config.viewport_width, height=self.config.viewport_height)
            dpg.setup_dearpygui()
            dpg.show_viewport()
            dpg.set_primary_window(self.root_tag, True)
            self._sync_widgets_from_state()
            self._render_placeholder()

            if initial_source is not None:
                self.load_source(initial_source)

            self._last_tick = time.perf_counter()
            while dpg.is_dearpygui_running():
                now = time.perf_counter()
                delta_seconds = now - self._last_tick
                self._last_tick = now
                self._tick(delta_seconds)
                dpg.render_dearpygui_frame()
        finally:
            dpg.destroy_context()

    def _build_ui(self) -> None:
        with dpg.texture_registry(tag=self.texture_registry_tag):
            pass

        with dpg.file_dialog(
            directory_selector=True,
            show=False,
            callback=self._on_directory_selected,
            cancel_callback=self._on_dialog_cancel,
            tag=self.dialog_tag,
            width=720,
            height=420,
        ):
            pass

        with dpg.window(tag=self.root_tag, label="Sprite Preview", width=self.config.viewport_width, height=self.config.viewport_height):
            dpg.add_text("Select a folder to load frames with natural filename ordering.")
            with dpg.group(horizontal=True):
                dpg.add_input_text(tag=self.source_input_tag, width=760, hint="Folder or single image path")
                dpg.add_button(label="Browse", callback=self._show_directory_dialog)
                dpg.add_button(label="Load", callback=self._load_from_input)
            with dpg.group(horizontal=True):
                dpg.add_text("Mode")
                dpg.add_combo(
                    items=["FPS", "Duration"],
                    default_value="Duration",
                    tag=self.mode_tag,
                    callback=self._on_mode_changed,
                    width=130,
                )
                dpg.add_input_float(tag=self.fps_tag, label="FPS", default_value=self.config.default_fps, min_value=0.1, step=1.0, width=110, callback=self._on_fps_changed)
                dpg.add_input_float(tag=self.duration_tag, label="Duration (s)", default_value=self.config.default_duration_seconds, min_value=0.01, step=0.1, width=140, callback=self._on_duration_changed)
                dpg.add_checkbox(tag=self.loop_tag, label="Loop", default_value=self.config.default_loop, callback=self._on_loop_changed)
                dpg.add_checkbox(tag=self.autoplay_tag, label="Autoplay", default_value=self.config.default_autoplay, callback=self._on_autoplay_changed)
            with dpg.group(horizontal=True):
                dpg.add_button(label="Previous Frame", callback=self._step_previous)
                dpg.add_button(label="Next Frame", callback=self._step_next)
                dpg.add_button(label="Reset", callback=self._reset_playhead)
                dpg.add_button(label="Play", tag=self.play_button_tag, callback=self._toggle_playback)
            dpg.add_text("", tag=self.status_tag)
            dpg.add_text("", tag=self.detail_tag)
            dpg.add_text("", tag=self.frame_tag)
            dpg.add_text("", tag=self.count_tag)
            dpg.add_spacer(height=8)
            with dpg.child_window(width=self.config.preview_width + 16, height=self.config.preview_height + 16, border=True):
                with dpg.drawlist(tag=self.preview_drawlist_tag, width=self.config.preview_width, height=self.config.preview_height):
                    pass

    def _show_directory_dialog(self) -> None:
        dpg.show_item(self.dialog_tag)

    def _load_from_input(self) -> None:
        source_text = str(dpg.get_value(self.source_input_tag)).strip().strip('"').strip("'")
        if not source_text:
            self._set_status("Enter a folder or image path.")
            return
        self.load_source(Path(source_text))

    def _on_directory_selected(self, sender: Any, app_data: Any, user_data: Any | None = None) -> None:
        source = self._extract_path_from_dialog_data(app_data)
        if source is None:
            self._set_status("Could not read a path from the file dialog.")
            return
        dpg.set_value(self.source_input_tag, str(source))
        self.load_source(source)

    def _on_dialog_cancel(self, sender: Any, app_data: Any, user_data: Any | None = None) -> None:
        self._set_status("Selection canceled.")

    def _on_mode_changed(self, sender: Any, app_data: Any, user_data: Any | None = None) -> None:
        value = str(app_data)
        if value == "FPS":
            self.playback.mode = "fps"
        else:
            self.playback.mode = "duration"
        self._sync_mode_widgets()
        self._refresh_ui()

    def _on_fps_changed(self, sender: Any, app_data: Any, user_data: Any | None = None) -> None:
        self.playback.fps = max(float(app_data), 0.1)
        self._refresh_ui()

    def _on_duration_changed(self, sender: Any, app_data: Any, user_data: Any | None = None) -> None:
        self.playback.duration_seconds = max(float(app_data), 0.01)
        self._refresh_ui()

    def _on_loop_changed(self, sender: Any, app_data: Any, user_data: Any | None = None) -> None:
        self.playback.loop = bool(app_data)

    def _on_autoplay_changed(self, sender: Any, app_data: Any, user_data: Any | None = None) -> None:
        self._autoplay_enabled = bool(app_data)

    def _toggle_playback(self) -> None:
        if self.sequence is None:
            self._set_status("Load a sequence first.")
            return
        self.playback.playing = not self.playback.playing
        self._sync_play_button()
        self._set_status("Playing." if self.playback.playing else "Paused.")

    def _step_previous(self) -> None:
        if self.sequence is None:
            self._set_status("Load a sequence first.")
            return
        self.playback.step_frames(-1, len(self.sequence.frames))
        self._sync_play_button()
        self._refresh_ui()

    def _step_next(self) -> None:
        if self.sequence is None:
            self._set_status("Load a sequence first.")
            return
        self.playback.step_frames(1, len(self.sequence.frames))
        self._sync_play_button()
        self._refresh_ui()

    def _reset_playhead(self) -> None:
        self.playback.reset()
        self.playback.playing = self._autoplay_enabled
        self._sync_play_button()
        self._refresh_ui()

    def load_source(self, source: Path) -> None:
        normalized_source = source.expanduser()
        if not normalized_source.is_absolute():
            normalized_source = Path.cwd() / normalized_source
        normalized_source = normalized_source.resolve(strict=False)

        try:
            frame_sources, warnings = load_frame_sources(normalized_source)
        except Exception as exc:  # noqa: BLE001
            self._set_status(str(exc))
            return

        if not frame_sources:
            self._set_status(f"No supported images found in {normalized_source}.")
            return

        self._load_generation += 1
        load_prefix = f"sprite_preview_{self._load_generation}"
        new_assets: list[FrameAsset] = []
        new_texture_tags: list[str] = []

        try:
            for index, frame_source in enumerate(frame_sources):
                texture_tag = f"{load_prefix}_frame_{index}"
                dpg.add_static_texture(
                    width=frame_source.width,
                    height=frame_source.height,
                    default_value=frame_source.pixel_data,
                    tag=texture_tag,
                    parent=self.texture_registry_tag,
                )
                new_texture_tags.append(texture_tag)
                new_assets.append(
                    FrameAsset(
                        path=frame_source.path,
                        width=frame_source.width,
                        height=frame_source.height,
                        texture_tag=texture_tag,
                    )
                )
        except Exception as exc:  # noqa: BLE001
            for texture_tag in new_texture_tags:
                self._safe_delete_item(texture_tag)
            self._set_status(f"Failed to create textures: {exc}")
            return

        old_sequence = self.sequence
        self.sequence = SpriteSequence(source=normalized_source, frames=new_assets, warnings=warnings)

        if old_sequence is not None:
            for frame in old_sequence.frames:
                self._safe_delete_item(frame.texture_tag)

        self.playback.reset()
        self.playback.playing = self._autoplay_enabled
        self._sync_mode_widgets()
        self._sync_play_button()
        self._refresh_ui()

        if warnings:
            self._set_status(f"Loaded {len(new_assets)} frame(s). Skipped {len(warnings)} file(s).")
        else:
            self._set_status(f"Loaded {len(new_assets)} frame(s).")

    def _tick(self, delta_seconds: float) -> None:
        if self.sequence is not None and self.playback.playing:
            self.playback.advance(delta_seconds, len(self.sequence.frames))
            self._sync_play_button()
        self._refresh_ui()

    def _refresh_ui(self) -> None:
        self._sync_mode_widgets()
        self._render_preview()
        self._update_texts()

    def _sync_widgets_from_state(self) -> None:
        self._sync_mode_widgets()
        self._sync_play_button()
        dpg.set_value(self.status_tag, "")
        dpg.set_value(self.detail_tag, "Status: No sequence loaded")
        dpg.set_value(self.frame_tag, "Frame: -")
        dpg.set_value(self.count_tag, "Total: 0")

    def _update_texts(self) -> None:
        if self.sequence is None:
            dpg.set_value(self.detail_tag, "Status: No sequence loaded")
            dpg.set_value(self.frame_tag, "Frame: -")
            dpg.set_value(self.count_tag, "Total: 0")
            return

        frame_count = len(self.sequence.frames)
        current_index = self.playback.current_frame_index if frame_count > 0 else 0
        current_index = max(0, min(frame_count - 1, current_index))
        current_frame = self.sequence.frames[current_index]
        total_duration = self.playback.total_duration(frame_count)
        frame_duration = self.playback.frame_duration(frame_count)

        if self.playback.mode == "fps":
            detail = f"Mode: FPS | FPS: {self.playback.fps:.2f} | Total Duration: {total_duration:.3f}s | {('Playing' if self.playback.playing else 'Paused')}"
        else:
            effective_fps = frame_count / max(self.playback.duration_seconds, 0.0001)
            detail = f"Mode: Duration | Total Duration: {self.playback.duration_seconds:.3f}s | Effective FPS: {effective_fps:.2f} | {('Playing' if self.playback.playing else 'Paused')}"

        dpg.set_value(self.detail_tag, detail)
        dpg.set_value(self.frame_tag, f"Current Frame: {current_index + 1}/{frame_count} | File: {current_frame.path.name} | Frame Duration: {frame_duration:.3f}s")
        dpg.set_value(self.count_tag, f"Source: {self.sequence.source}")

    def _render_placeholder(self) -> None:
        dpg.delete_item(self.preview_drawlist_tag, children_only=True)
        dpg.draw_rectangle(
            pmin=(0, 0),
            pmax=(self.config.preview_width, self.config.preview_height),
            fill=(24, 24, 28, 255),
            color=(64, 64, 72, 255),
            parent=self.preview_drawlist_tag,
        )
        dpg.draw_text(
            (24, 24),
            "Select a folder that contains sprite frames.",
            color=(230, 230, 230, 255),
            size=18,
            parent=self.preview_drawlist_tag,
        )

    def _render_preview(self) -> None:
        if self.sequence is None or not self.sequence.frames:
            self._render_placeholder()
            return

        frame_count = len(self.sequence.frames)
        current_index = self.playback.current_index_from_time(frame_count)
        self.playback.current_frame_index = current_index
        frame = self.sequence.frames[current_index]

        canvas_width = self.config.preview_width
        canvas_height = self.config.preview_height
        scale = min(canvas_width / frame.width, canvas_height / frame.height)
        draw_width = frame.width * scale
        draw_height = frame.height * scale
        x1 = (canvas_width - draw_width) * 0.5
        y1 = (canvas_height - draw_height) * 0.5
        x2 = x1 + draw_width
        y2 = y1 + draw_height

        dpg.delete_item(self.preview_drawlist_tag, children_only=True)
        dpg.draw_rectangle(
            pmin=(0, 0),
            pmax=(canvas_width, canvas_height),
            fill=(18, 18, 22, 255),
            color=(72, 72, 80, 255),
            parent=self.preview_drawlist_tag,
        )
        dpg.draw_image(
            frame.texture_tag,
            (x1, y1),
            (x2, y2),
            parent=self.preview_drawlist_tag,
        )
        dpg.draw_text(
            (16, 14),
            f"{current_index + 1}/{frame_count}  {frame.path.name}",
            color=(245, 245, 245, 255),
            size=18,
            parent=self.preview_drawlist_tag,
        )
        dpg.draw_text(
            (16, canvas_height - 28),
            f"{frame.width}x{frame.height}",
            color=(210, 210, 210, 255),
            size=16,
            parent=self.preview_drawlist_tag,
        )

    def _sync_mode_widgets(self) -> None:
        dpg.set_value(self.mode_tag, "FPS" if self.playback.mode == "fps" else "Duration")
        dpg.set_value(self.fps_tag, self.playback.fps)
        dpg.set_value(self.duration_tag, self.playback.duration_seconds)
        dpg.configure_item(self.fps_tag, enabled=self.playback.mode == "fps")
        dpg.configure_item(self.duration_tag, enabled=self.playback.mode == "duration")
        dpg.set_value(self.loop_tag, self.playback.loop)
        dpg.set_value(self.autoplay_tag, self._autoplay_enabled)

    def _sync_play_button(self) -> None:
        dpg.configure_item(self.play_button_tag, label="Pause" if self.playback.playing else "Play")

    def _set_status(self, text: str) -> None:
        dpg.set_value(self.status_tag, text)

    def _safe_delete_item(self, tag: str) -> None:
        try:
            dpg.delete_item(tag)
        except Exception:  # noqa: BLE001
            pass

    def _extract_path_from_dialog_data(self, app_data: Any) -> Path | None:
        if isinstance(app_data, (str, Path)):
            return Path(app_data)

        if isinstance(app_data, dict):
            for key in ("file_path_name", "current_path", "path"):
                value = app_data.get(key)
                if value:
                    return Path(value)

            selections = app_data.get("selections")
            if isinstance(selections, dict) and selections:
                first_value = next(iter(selections.values()))
                if first_value:
                    return Path(first_value)
            if isinstance(selections, (list, tuple)) and selections:
                first_value = selections[0]
                if first_value:
                    return Path(first_value)

            for value in app_data.values():
                if isinstance(value, (str, Path)) and value:
                    return Path(value)

        return None
