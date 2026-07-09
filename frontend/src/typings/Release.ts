import type DownloadProtocol from 'DownloadClient/DownloadProtocol';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import CustomFormat from 'typings/CustomFormat';

export interface ReleaseAudioInfo {
  language?: string;
  specification?: string;
}

interface Release {
  guid: string;
  protocol: DownloadProtocol;
  age: number;
  ageHours: number;
  ageMinutes: number;
  publishDate: string;
  title: string;
  infoUrl: string;
  indexerId: number;
  prowlarrIndexerId?: number;
  indexer: string;
  size: number;
  seeders?: number;
  leechers?: number;
  quality: QualityModel;
  languages: Language[];
  subs: string[];
  audioInfo: ReleaseAudioInfo[];
  preferredAudioInfo?: ReleaseAudioInfo;
  audioPreferenceScore?: number;
  hasChineseAudioOrSubtitle?: boolean;
  mediaInfoStatus?: string;
  mediaInfoHandleId?: string;
  mediaInfoSearchId?: string;
  mediaInfoProgressStatus?: string;
  mediaInfoProgressCompleted?: number;
  mediaInfoProgressTotal?: number;
  customFormats: CustomFormat[];
  customFormatScore: number;
  mappedMovieId?: number;
  indexerFlags: string[];
  rejections: string[];
  movieRequested: boolean;
  downloadAllowed: boolean;

  isGrabbing?: boolean;
  isGrabbed?: boolean;
  grabError?: string;
}

export default Release;
