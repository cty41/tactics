from __future__ import annotations

from dataclasses import dataclass

from .models import PlaybackMode


def _clamp(value: int, minimum: int, maximum: int) -> int:
    return max(minimum, min(maximum, value))


@dataclass(slots=True)
class PlaybackState:
    mode: PlaybackMode = "duration"
    fps: float = 6.0
    duration_seconds: float = 1.0
    loop: bool = True
    playing: bool = False
    playhead_seconds: float = 0.0
    current_frame_index: int = 0

    def reset(self) -> None:
        self.playhead_seconds = 0.0
        self.current_frame_index = 0
        self.playing = False

    def set_frame(self, frame_index: int, frame_count: int) -> None:
        if frame_count <= 0:
            self.current_frame_index = 0
            self.playhead_seconds = 0.0
            return

        frame_index = _clamp(frame_index, 0, frame_count - 1)
        self.current_frame_index = frame_index
        frame_duration = self.frame_duration(frame_count)
        self.playhead_seconds = frame_index * frame_duration

    def step_frames(self, delta_frames: int, frame_count: int) -> None:
        if frame_count <= 0:
            self.reset()
            return

        self.playing = False
        next_index = self.current_frame_index + delta_frames
        if self.loop:
            next_index %= frame_count
        else:
            next_index = _clamp(next_index, 0, frame_count - 1)
        self.set_frame(next_index, frame_count)

    def frame_duration(self, frame_count: int) -> float:
        if frame_count <= 0:
            return 0.0
        if self.mode == "fps":
            return 1.0 / max(self.fps, 0.0001)
        return max(self.duration_seconds, 0.0001) / frame_count

    def total_duration(self, frame_count: int) -> float:
        if frame_count <= 0:
            return 0.0
        if self.mode == "fps":
            return frame_count * self.frame_duration(frame_count)
        return max(self.duration_seconds, 0.0001)

    def current_index_from_time(self, frame_count: int) -> int:
        if frame_count <= 0:
            return 0
        frame_duration = self.frame_duration(frame_count)
        if frame_duration <= 0:
            return 0

        index = int(self.playhead_seconds / frame_duration)
        if self.loop:
            return index % frame_count
        return _clamp(index, 0, frame_count - 1)

    def advance(self, delta_seconds: float, frame_count: int) -> None:
        if not self.playing or frame_count <= 0:
            return

        total_duration = self.total_duration(frame_count)
        if total_duration <= 0:
            return

        self.playhead_seconds += delta_seconds
        if self.loop:
            self.playhead_seconds %= total_duration
        elif self.playhead_seconds >= total_duration:
            self.playhead_seconds = total_duration
            self.playing = False

        self.current_frame_index = self.current_index_from_time(frame_count)
