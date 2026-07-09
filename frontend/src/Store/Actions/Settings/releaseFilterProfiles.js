import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.releaseFilterProfiles';

//
// Actions Types

export const FETCH_RELEASE_FILTER_PROFILES = 'settings/releaseFilterProfiles/fetchReleaseFilterProfiles';

//
// Action Creators

export const fetchReleaseFilterProfiles = createThunk(FETCH_RELEASE_FILTER_PROFILES);

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
    [FETCH_RELEASE_FILTER_PROFILES]: createFetchHandler(section, '/releasefilterprofile')
  },

  //
  // Reducers

  reducers: {}

};
