import React, { useCallback, useState } from 'react';
import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import AppState from 'App/State/AppState';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import SpinnerIcon from 'Components/SpinnerIcon';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import MovieFormats from 'Movie/MovieFormats';
import MovieLanguages from 'Movie/MovieLanguages';
import MovieQuality from 'Movie/MovieQuality';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import Release, { ReleaseAudioInfo } from 'typings/Release';
import formatDateTime from 'Utilities/Date/formatDateTime';
import formatAge from 'Utilities/Number/formatAge';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import InteractiveSearchPayload from './InteractiveSearchPayload';
import OverrideMatchModal from './OverrideMatch/OverrideMatchModal';
import Peers from './Peers';
import styles from './InteractiveSearchRow.css';

function getDownloadIcon(
  isGrabbing: boolean,
  isGrabbed: boolean,
  grabError?: string
) {
  if (isGrabbing) {
    return icons.SPINNER;
  } else if (isGrabbed) {
    return icons.DOWNLOADING;
  } else if (grabError) {
    return icons.DOWNLOADING;
  }

  return icons.DOWNLOAD;
}

function getDownloadKind(isGrabbed: boolean, grabError?: string) {
  if (isGrabbed) {
    return kinds.SUCCESS;
  }

  if (grabError) {
    return kinds.DANGER;
  }

  return kinds.DEFAULT;
}

function getDownloadTooltip(
  isGrabbing: boolean,
  isGrabbed: boolean,
  grabError?: string
) {
  if (isGrabbing) {
    return '';
  } else if (isGrabbed) {
    return translate('AddedToDownloadQueue');
  } else if (grabError) {
    return grabError;
  }

  return translate('AddToDownloadQueue');
}

function formatAudioInfo(audioInfo: ReleaseAudioInfo, displayLanguage?: string) {
  const language = (
    displayLanguage ||
    audioInfo.mappedLanguage?.name ||
    audioInfo.language
  )?.trim();
  const specification = audioInfo.specification?.trim();

  if (language && specification) {
    return `${language}: ${specification}`;
  }

  return language || specification || '';
}

function releaseHistorySelector({ guid }: Release) {
  return createSelector(
    (state: AppState) => state.movieHistory.items,
    (state: AppState) => state.movieBlocklist.items,
    (movieHistory, movieBlocklist) => {
      let historyFailedData = null;
      let blocklistedData = null;

      const historyGrabbedData = movieHistory.find(
        ({ eventType, data }) =>
          eventType === 'grabbed' && 'guid' in data && data.guid === guid
      );

      if (historyGrabbedData) {
        historyFailedData = movieHistory.find(
          ({ eventType, sourceTitle }) =>
            eventType === 'downloadFailed' &&
            sourceTitle === historyGrabbedData.sourceTitle
        );

        blocklistedData = movieBlocklist.find(
          (item) => item.sourceTitle === historyGrabbedData.sourceTitle
        );
      }

      return {
        historyGrabbedData,
        historyFailedData,
        blocklistedData,
      };
    }
  );
}

interface InteractiveSearchRowProps extends Release {
  searchPayload: InteractiveSearchPayload;
  onGrabPress(...args: unknown[]): void;
}

function InteractiveSearchRow(props: InteractiveSearchRowProps) {
  const {
    guid,
    indexerId,
    protocol,
    age,
    ageHours,
    ageMinutes,
    publishDate,
    title,
    infoUrl,
    indexer,
    size,
    seeders,
    leechers,
    quality,
    languages,
    subs = [],
    audioInfo = [],
    selectedAudioInfo,
    selectedAudioLanguage,
    selectedAudioTags = [],
    audioScore = 0,
    audioScoreBreakdown = [],
    audioLanguagePreferenceName,
    mediaInfoStatus,
    customFormatScore,
    customFormats,
    mappedMovieId,
    indexerFlags = [],
    rejections = [],
    downloadAllowed,
    isGrabbing = false,
    isGrabbed = false,
    grabError,
    searchPayload,
    onGrabPress,
  } = props;

  const { longDateFormat, timeFormat } = useSelector(
    createUISettingsSelector()
  );

  const { historyGrabbedData, historyFailedData, blocklistedData } =
    useSelector(releaseHistorySelector(props));

  const [isConfirmGrabModalOpen, setIsConfirmGrabModalOpen] = useState(false);
  const [isOverrideModalOpen, setIsOverrideModalOpen] = useState(false);
  const audioDetails = audioInfo.map((audio) => formatAudioInfo(audio)).filter(Boolean);
  const audioLabel =
    audioDetails.length > 1
      ? translate('MultiLanguage')
      : audioInfo[0]?.language?.trim() || audioDetails[0];
  const selectedAudioLabel = selectedAudioInfo
    ? formatAudioInfo(selectedAudioInfo, selectedAudioLanguage)
    : '';
  const selectedAudioDetails = [
    selectedAudioLabel,
    selectedAudioTags.length ? `${translate('Tags')}: ${selectedAudioTags.join(', ')}` : null,
    audioLanguagePreferenceName ? `${translate('AudioLanguagePreference')}: ${audioLanguagePreferenceName}` : null,
  ].filter(Boolean);
  const audioScoreLabel = audioScore > 0 ? `+${audioScore}` : `${audioScore}`;
  const subtitleLabel =
    subs.length > 1 ? translate('MultiLanguage') : subs[0];
  const isMediaInfoPending = mediaInfoStatus === 'pending';
  const isAudioInfoLoading = isMediaInfoPending && !audioDetails.length;
  const isSubsLoading = isMediaInfoPending && !subs.length;

  const onGrabPressWrapper = useCallback(() => {
    if (downloadAllowed) {
      onGrabPress({
        guid,
        indexerId,
      });

      return;
    }

    setIsConfirmGrabModalOpen(true);
  }, [
    guid,
    indexerId,
    downloadAllowed,
    onGrabPress,
    setIsConfirmGrabModalOpen,
  ]);

  const onGrabConfirm = useCallback(() => {
    setIsConfirmGrabModalOpen(false);

    onGrabPress({
      guid,
      indexerId,
      ...searchPayload,
    });
  }, [guid, indexerId, searchPayload, onGrabPress, setIsConfirmGrabModalOpen]);

  const onGrabCancel = useCallback(() => {
    setIsConfirmGrabModalOpen(false);
  }, [setIsConfirmGrabModalOpen]);

  const onOverridePress = useCallback(() => {
    setIsOverrideModalOpen(true);
  }, [setIsOverrideModalOpen]);

  const onOverrideModalClose = useCallback(() => {
    setIsOverrideModalOpen(false);
  }, [setIsOverrideModalOpen]);

  return (
    <TableRow>
      <TableRowCell className={styles.protocol}>
        <ProtocolLabel protocol={protocol} />
      </TableRowCell>

      <TableRowCell
        className={styles.age}
        title={formatDateTime(publishDate, longDateFormat, timeFormat, {
          includeSeconds: true,
        })}
      >
        {formatAge(age, ageHours, ageMinutes)}
      </TableRowCell>

      <TableRowCell>
        <div className={styles.titleContent}>
          <Link to={infoUrl} title={title}>
            {title}
          </Link>
        </div>
      </TableRowCell>

      <TableRowCell className={styles.indexer}>{indexer}</TableRowCell>

      <TableRowCell className={styles.history}>
        {historyGrabbedData?.date && !historyFailedData?.date ? (
          <Tooltip
            anchor={<Icon name={icons.DOWNLOADING} kind={kinds.DEFAULT} />}
            tooltip={translate('GrabbedAt', {
              date: formatDateTime(
                historyGrabbedData.date,
                longDateFormat,
                timeFormat,
                { includeSeconds: true }
              ),
            })}
            kind={kinds.INVERSE}
            position={tooltipPositions.LEFT}
          />
        ) : null}

        {historyFailedData?.date ? (
          <Tooltip
            anchor={<Icon name={icons.DOWNLOADING} kind={kinds.DANGER} />}
            tooltip={translate('FailedAt', {
              date: formatDateTime(
                historyFailedData.date,
                longDateFormat,
                timeFormat,
                { includeSeconds: true }
              ),
            })}
            kind={kinds.INVERSE}
            position={tooltipPositions.LEFT}
          />
        ) : null}

        {blocklistedData?.date ? (
          <Icon
            className={
              historyGrabbedData || historyFailedData ? styles.blocklist : ''
            }
            name={icons.BLOCKLIST}
            kind={kinds.DANGER}
            title={translate('BlocklistedAt', {
              date: formatDateTime(
                blocklistedData.date,
                longDateFormat,
                timeFormat,
                { includeSeconds: true }
              ),
            })}
          />
        ) : null}
      </TableRowCell>

      <TableRowCell className={styles.size}>{formatBytes(size)}</TableRowCell>

      <TableRowCell className={styles.peers}>
        {protocol === 'torrent' ? (
          <Peers seeders={seeders} leechers={leechers} />
        ) : null}
      </TableRowCell>

      <TableRowCell className={styles.languages}>
        <MovieLanguages languages={languages} />
      </TableRowCell>

      <TableRowCell className={styles.audioInfo}>
        <span className={styles.mediaCellContent}>
          {audioDetails.length ? (
            <Popover
              anchor={
                <Label kind={kinds.INVERSE}>
                  {audioLabel || translate('AudioInfo')}
                </Label>
              }
              title={translate('AudioInfo')}
              body={
                <ul>
                  {audioDetails.map((audio, index) => {
                    return <li key={index}>{audio}</li>;
                  })}
                </ul>
              }
              position={tooltipPositions.LEFT}
            />
          ) : null}

          {isAudioInfoLoading ? (
            <SpinnerIcon
              className={styles.mediaLoadingIcon}
              name={icons.SPINNER}
              isSpinning={true}
            />
          ) : null}
        </span>
      </TableRowCell>

      <TableRowCell className={styles.selectedAudio}>
        <span className={styles.mediaCellContent}>
          {selectedAudioLabel ? (
            <Popover
              anchor={<Label kind={kinds.INVERSE}>{selectedAudioLabel}</Label>}
              title={translate('SelectedAudio')}
              body={
                <ul>
                  {selectedAudioDetails.map((detail, index) => {
                    return <li key={index}>{detail}</li>;
                  })}
                </ul>
              }
              position={tooltipPositions.LEFT}
            />
          ) : null}

          {isAudioInfoLoading ? (
            <SpinnerIcon
              className={styles.mediaLoadingIcon}
              name={icons.SPINNER}
              isSpinning={true}
            />
          ) : null}
        </span>
      </TableRowCell>

      <TableRowCell className={styles.audioScore}>
        {audioScoreBreakdown.length ? (
          <Popover
            anchor={audioScoreLabel}
            title={translate('AudioScoreBreakdown')}
            body={
              <ul>
                {audioScoreBreakdown.map((score, index) => {
                  return <li key={index}>{score}</li>;
                })}
              </ul>
            }
            position={tooltipPositions.LEFT}
          />
        ) : (
          audioScoreLabel
        )}
      </TableRowCell>

      <TableRowCell className={styles.subs}>
        <span className={styles.mediaCellContent}>
          {subs.length ? (
            <Popover
              anchor={<Label kind={kinds.INVERSE}>{subtitleLabel}</Label>}
              title={translate('SubtitleLanguages')}
              body={
                <ul>
                  {subs.map((subtitle, index) => {
                    return <li key={index}>{subtitle}</li>;
                  })}
                </ul>
              }
              position={tooltipPositions.LEFT}
            />
          ) : null}

          {isSubsLoading ? (
            <SpinnerIcon
              className={styles.mediaLoadingIcon}
              name={icons.SPINNER}
              isSpinning={true}
            />
          ) : null}
        </span>
      </TableRowCell>

      <TableRowCell className={styles.quality}>
        <MovieQuality quality={quality} showRevision={true} />
      </TableRowCell>

      <TableRowCell className={styles.customFormatScore}>
        <Tooltip
          anchor={formatCustomFormatScore(
            customFormatScore,
            customFormats.length
          )}
          tooltip={<MovieFormats formats={customFormats} />}
          position={tooltipPositions.LEFT}
        />
      </TableRowCell>

      <TableRowCell className={styles.indexerFlags}>
        {indexerFlags.length ? (
          <Popover
            anchor={<Icon name={icons.FLAG} />}
            title={translate('IndexerFlags')}
            body={
              <ul>
                {indexerFlags.map((flag, index) => {
                  return <li key={index}>{flag}</li>;
                })}
              </ul>
            }
            position={tooltipPositions.LEFT}
          />
        ) : null}
      </TableRowCell>

      <TableRowCell className={styles.rejected}>
        {rejections.length ? (
          <Popover
            anchor={<Icon name={icons.DANGER} kind={kinds.DANGER} />}
            title={translate('ReleaseRejected')}
            body={
              <ul>
                {rejections.map((rejection, index) => {
                  return <li key={index}>{rejection}</li>;
                })}
              </ul>
            }
            position={tooltipPositions.LEFT}
          />
        ) : null}
      </TableRowCell>

      <TableRowCell className={styles.download}>
        <SpinnerIconButton
          name={getDownloadIcon(isGrabbing, isGrabbed, grabError)}
          kind={getDownloadKind(isGrabbed, grabError)}
          title={getDownloadTooltip(isGrabbing, isGrabbed, grabError)}
          isSpinning={isGrabbing}
          onPress={onGrabPressWrapper}
        />

        <Link
          className={styles.manualDownloadContent}
          title={translate('OverrideAndAddToDownloadQueue')}
          onPress={onOverridePress}
        >
          <div className={styles.manualDownloadContent}>
            <Icon
              className={styles.interactiveIcon}
              name={icons.INTERACTIVE}
              size={12}
            />

            <Icon
              className={styles.downloadIcon}
              name={icons.CIRCLE_DOWN}
              size={10}
            />
          </div>
        </Link>
      </TableRowCell>

      <ConfirmModal
        isOpen={isConfirmGrabModalOpen}
        kind={kinds.WARNING}
        title={translate('GrabRelease')}
        message={translate('GrabReleaseMessageText', { title })}
        confirmLabel={translate('Grab')}
        onConfirm={onGrabConfirm}
        onCancel={onGrabCancel}
      />

      <OverrideMatchModal
        isOpen={isOverrideModalOpen}
        title={title}
        indexerId={indexerId}
        guid={guid}
        movieId={mappedMovieId}
        languages={languages}
        quality={quality}
        protocol={protocol}
        isGrabbing={isGrabbing}
        grabError={grabError}
        onModalClose={onOverrideModalClose}
      />
    </TableRow>
  );
}

export default InteractiveSearchRow;
