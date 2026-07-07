import AppSectionState, {
  AppSectionFilterState,
} from 'App/State/AppSectionState';
import Release from 'typings/Release';

interface ReleasesAppState
  extends AppSectionState<Release>,
    AppSectionFilterState<Release> {
  isMediaInfoFetching: boolean;
  isMediaInfoComplete: boolean;
  mediaInfoTotal: number;
  mediaInfoCompleted: number;
}

export default ReleasesAppState;
