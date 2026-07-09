import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.audioScoreProfiles';

//
// Actions Types

export const FETCH_AUDIO_SCORE_PROFILES = 'settings/audioScoreProfiles/fetchAudioScoreProfiles';

//
// Action Creators

export const fetchAudioScoreProfiles = createThunk(FETCH_AUDIO_SCORE_PROFILES);

//
// Details

export default {

  //
  // State

  defaultState: {
    isFetching: false,
    isPopulated: false,
    error: null,
    items: []
  },

  //
  // Action Handlers

  actionHandlers: {
    [FETCH_AUDIO_SCORE_PROFILES]: createFetchHandler(section, '/audioscoreprofile')
  },

  //
  // Reducers

  reducers: {}

};
