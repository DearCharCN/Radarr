import createFetchHandler from 'Store/Actions/Creators/createFetchHandler';
import { createThunk } from 'Store/thunks';

//
// Variables

const section = 'settings.customFormatMutexGroups';

//
// Actions Types

export const FETCH_CUSTOM_FORMAT_MUTEX_GROUPS = 'settings/customFormatMutexGroups/fetchCustomFormatMutexGroups';

//
// Action Creators

export const fetchCustomFormatMutexGroups = createThunk(FETCH_CUSTOM_FORMAT_MUTEX_GROUPS);

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
    [FETCH_CUSTOM_FORMAT_MUTEX_GROUPS]: createFetchHandler(section, '/customformatmutexgroup')
  },

  //
  // Reducers

  reducers: {}

};
