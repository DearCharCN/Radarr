export interface AudioScoreRule {
  name: string;
  matchType: string;
  pattern: string;
  score: number;
  mutexGroup?: string;
  enabled: boolean;
}

export interface AudioScoreMutexGroup {
  name: string;
  enabled: boolean;
}

interface AudioScoreProfile {
  id: number;
  name: string;
  enabled: boolean;
  rules: AudioScoreRule[];
  mutexGroups: AudioScoreMutexGroup[];
}

export default AudioScoreProfile;
