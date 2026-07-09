export interface AudioLanguagePreferenceItem {
  languageTag: string;
  enabled: boolean;
}

interface AudioLanguagePreference {
  id: number;
  name: string;
  enabled: boolean;
  scoreGapThreshold: number;
  audioScoreProfileId?: number;
  entries: AudioLanguagePreferenceItem[];
}

export default AudioLanguagePreference;
