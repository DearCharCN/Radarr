import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.audioLanguagePreferences';

//
// Actions Types

export const FETCH_AUDIO_LANGUAGE_PREFERENCES = 'settings/audioLanguagePreferences/fetchAudioLanguagePreferences';

//
// Action Creators

export const fetchAudioLanguagePreferences = createThunk(FETCH_AUDIO_LANGUAGE_PREFERENCES);

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
    [FETCH_AUDIO_LANGUAGE_PREFERENCES]: createFetchHandler(section, '/audiolanguagepreference')
  },

  //
  // Reducers

  reducers: {}

};
