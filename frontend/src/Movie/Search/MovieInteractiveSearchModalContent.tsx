import React, { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import ReleasesAppState from 'App/State/ReleasesAppState';
import Button from 'Components/Link/Button';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { scrollDirections } from 'Helpers/Props';
import InteractiveSearch from 'InteractiveSearch/InteractiveSearch';
import Movie from 'Movie/Movie';
import useMovie from 'Movie/useMovie';
import { clearMovieBlocklist } from 'Store/Actions/movieBlocklistActions';
import { clearMovieHistory } from 'Store/Actions/movieHistoryActions';
import {
  cancelFetchReleases,
  clearReleases,
} from 'Store/Actions/releaseActions';
import translate from 'Utilities/String/translate';
import styles from './MovieInteractiveSearchModalContent.css';

export interface MovieInteractiveSearchModalContentProps {
  movieId: number;
  onModalClose(): void;
}

function MovieInteractiveSearchModalContent({
  movieId,
  onModalClose,
}: MovieInteractiveSearchModalContentProps) {
  const dispatch = useDispatch();

  const { title, year } = useMovie(movieId) as Movie;
  const {
    isPopulated,
    isMediaInfoFetching,
    isMediaInfoComplete,
    mediaInfoTotal,
    mediaInfoCompleted,
  } = useSelector((state: { releases: ReleasesAppState }) => state.releases);

  useEffect(() => {
    return () => {
      dispatch(cancelFetchReleases());
      dispatch(clearReleases());

      dispatch(clearMovieBlocklist());
      dispatch(clearMovieHistory());
    };
  }, [dispatch]);

  const movieTitle = `${title}${year > 0 ? ` (${year})` : ''}`;
  const showMediaInfoProgress =
    isPopulated &&
    mediaInfoTotal > 0 &&
    (isMediaInfoFetching || isMediaInfoComplete);

  let mediaInfoProgressLabel = '';

  if (isMediaInfoFetching) {
    mediaInfoProgressLabel = translate('QueryingAdditionalDataProgress', {
      completed: mediaInfoCompleted,
      total: mediaInfoTotal,
    });
  } else if (isMediaInfoComplete) {
    mediaInfoProgressLabel = translate('AdditionalDataComplete');
  }

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {movieTitle
          ? translate('InteractiveSearchModalHeaderTitle', {
              title: movieTitle,
            })
          : translate('InteractiveSearchModalHeader')}
      </ModalHeader>

      <ModalBody scrollDirection={scrollDirections.BOTH}>
        <InteractiveSearch searchPayload={{ movieId }} />
      </ModalBody>

      <ModalFooter className={styles.modalFooter}>
        <div className={styles.mediaInfoProgress}>
          {showMediaInfoProgress ? mediaInfoProgressLabel : null}
        </div>

        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default MovieInteractiveSearchModalContent;
